import argparse
import asyncio
import json
import logging
import os
import struct
from dataclasses import dataclass
from typing import Optional

import discord

FRAME_SIZE = 3840


class PcmMemoryAudioSource(discord.AudioSource):
    def __init__(self, pcm_bytes: bytes):
        self._pcm = pcm_bytes
        self._offset = 0

    def read(self) -> bytes:
        if self._offset >= len(self._pcm):
            return b""

        end = min(self._offset + FRAME_SIZE, len(self._pcm))
        chunk = self._pcm[self._offset:end]
        self._offset = end
        if len(chunk) < FRAME_SIZE:
            chunk = chunk + (b"\x00" * (FRAME_SIZE - len(chunk)))
        return chunk

    def is_opus(self) -> bool:
        return False


@dataclass
class BridgeStatus:
    ok: bool
    error: Optional[str]
    bot_ready: bool
    user_name: Optional[str]

    def to_bytes(self) -> bytes:
        return json.dumps(
            {
                "ok": self.ok,
                "error": self.error,
                "botReady": self.bot_ready,
                "userName": self.user_name,
            }
        ).encode("utf-8")


class BridgeBot(discord.Client):
    def __init__(self, tracked_user_id: int):
        intents = discord.Intents.none()
        intents.guilds = True
        intents.voice_states = True
        intents.members = True

        super().__init__(intents=intents)
        self.tracked_user_id = tracked_user_id
        self.queue: asyncio.Queue[bytes] = asyncio.Queue()
        self.play_lock = asyncio.Lock()
        self.shutdown_event = asyncio.Event()

    @property
    def tracked_user_name(self) -> Optional[str]:
        for guild in self.guilds:
            member = guild.get_member(self.tracked_user_id)
            if member is not None:
                return member.name
        return None

    @property
    def current_voice_client(self) -> Optional[discord.VoiceClient]:
        for guild in self.guilds:
            if guild.voice_client is not None:
                return guild.voice_client
        return None

    async def on_ready(self) -> None:
        logging.info("Discord bot ready as %s (%s)", self.user, self.user.id if self.user else "unknown")
        await self.ensure_following_tracked_user()

    async def on_voice_state_update(self, member: discord.Member, before: discord.VoiceState, after: discord.VoiceState) -> None:
        if member.id != self.tracked_user_id:
            return

        if after.channel is None:
            logging.info("Tracked user left voice. Disconnecting bot.")
            await self.disconnect_voice()
            return

        logging.info("Tracked user moved/joined voice channel %s (%s)", after.channel.name, after.channel.id)
        await self.connect_to_channel(after.channel)

    async def ensure_following_tracked_user(self) -> None:
        for guild in self.guilds:
            for channel in guild.voice_channels:
                for member in channel.members:
                    if member.id == self.tracked_user_id:
                        logging.info("Tracked user already in %s (%s). Connecting.", channel.name, channel.id)
                        await self.connect_to_channel(channel)
                        return

            try:
                member = await guild.fetch_member(self.tracked_user_id)
            except Exception:
                member = None

            if member is not None and member.voice and member.voice.channel:
                logging.info("Tracked user fetched in %s (%s). Connecting.", member.voice.channel.name, member.voice.channel.id)
                await self.connect_to_channel(member.voice.channel)
                return

    async def connect_to_channel(self, channel: discord.VoiceChannel) -> None:
        voice = channel.guild.voice_client
        if voice is not None:
            if voice.channel and voice.channel.id == channel.id and voice.is_connected():
                logging.info("Voice already connected to %s (%s)", channel.name, channel.id)
                await self.play_next_if_idle()
                return

            try:
                logging.info("Moving/reconnecting voice to %s (%s)", channel.name, channel.id)
                await voice.move_to(channel)
            except Exception:
                logging.exception("move_to failed. Reconnecting cleanly.")
                try:
                    await voice.disconnect(force=True)
                except Exception:
                    logging.exception("disconnect during reconnect failed")
                voice = await channel.connect(self_deaf=True)
        else:
            logging.info("Connecting voice to %s (%s)", channel.name, channel.id)
            voice = await channel.connect(self_deaf=True)

        await self.play_next_if_idle()

    async def disconnect_voice(self) -> None:
        voice = self.current_voice_client
        if voice is None:
            return
        try:
            await voice.disconnect(force=True)
        except Exception:
            logging.exception("disconnect_voice failed")

    async def enqueue_audio(self, pcm_bytes: bytes) -> None:
        await self.queue.put(pcm_bytes)
        logging.info("Enqueued PCM bytes=%s queue_size=%s", len(pcm_bytes), self.queue.qsize())
        await self.play_next_if_idle()

    async def play_next_if_idle(self) -> None:
        async with self.play_lock:
            voice = self.current_voice_client
            if voice is None or not voice.is_connected():
                logging.info("play_next_if_idle skipped. No connected voice client.")
                return

            if voice.is_playing() or voice.is_paused():
                return

            if self.queue.empty():
                return

            pcm_bytes = await self.queue.get()
            source = PcmMemoryAudioSource(pcm_bytes)
            loop = asyncio.get_running_loop()
            finished: asyncio.Future[None] = loop.create_future()

            def after_playback(error: Optional[Exception]) -> None:
                if error is not None:
                    logging.exception("Playback error", exc_info=error)
                loop.call_soon_threadsafe(finished.set_result, None)

            logging.info("Starting playback bytes=%s remaining_queue=%s", len(pcm_bytes), self.queue.qsize())
            voice.play(source, after=after_playback)
            loop.create_task(self._await_playback_end(finished))

    async def _await_playback_end(self, finished: asyncio.Future[None]) -> None:
        await finished
        logging.info("Playback finished")
        await self.play_next_if_idle()

    async def close(self) -> None:
        await self.disconnect_voice()
        await super().close()
        self.shutdown_event.set()


class BridgeServer:
    def __init__(self, bot: BridgeBot, port: int):
        self.bot = bot
        self.port = port
        self.server: Optional[asyncio.AbstractServer] = None

    async def start(self) -> None:
        self.server = await asyncio.start_server(self.handle_client, "127.0.0.1", self.port)
        logging.info("Bridge server listening on 127.0.0.1:%s", self.port)

    async def stop(self) -> None:
        if self.server is not None:
            self.server.close()
            await self.server.wait_closed()
            self.server = None

    async def handle_client(self, reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
        try:
            header_size = struct.unpack("<I", await reader.readexactly(4))[0]
            header = json.loads((await reader.readexactly(header_size)).decode("utf-8"))
            payload_length = int(header.get("payloadLength", 0))
            payload = await reader.readexactly(payload_length) if payload_length > 0 else b""

            command = header.get("type")
            if command == "status":
                response = BridgeStatus(True, None, self.bot.is_ready(), self.bot.tracked_user_name)
            elif command == "enqueue_audio":
                await self.bot.enqueue_audio(payload)
                response = BridgeStatus(True, None, self.bot.is_ready(), self.bot.tracked_user_name)
            elif command == "shutdown":
                response = BridgeStatus(True, None, self.bot.is_ready(), self.bot.tracked_user_name)
                response_bytes = response.to_bytes()
                writer.write(struct.pack("<I", len(response_bytes)))
                writer.write(response_bytes)
                await writer.drain()
                await self.stop()
                await self.bot.close()
                return
            else:
                response = BridgeStatus(False, f"Unknown command: {command}", self.bot.is_ready(), self.bot.tracked_user_name)

            response_bytes = response.to_bytes()
            writer.write(struct.pack("<I", len(response_bytes)))
            writer.write(response_bytes)
            await writer.drain()
        except Exception as ex:
            logging.exception("Bridge server client handler failed")
            response = BridgeStatus(False, str(ex), self.bot.is_ready(), self.bot.tracked_user_name)
            response_bytes = response.to_bytes()
            try:
                writer.write(struct.pack("<I", len(response_bytes)))
                writer.write(response_bytes)
                await writer.drain()
            except Exception:
                pass
        finally:
            writer.close()
            await writer.wait_closed()


def configure_logging(log_file: str) -> None:
    os.makedirs(os.path.dirname(log_file), exist_ok=True)
    logging.basicConfig(
        level=logging.INFO,
        format="[%(asctime)s] [PyBridge] %(message)s",
        handlers=[
            logging.FileHandler(log_file, encoding="utf-8"),
            logging.StreamHandler(),
        ],
    )


async def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", required=True, type=int)
    parser.add_argument("--token", required=True)
    parser.add_argument("--user-id", required=True, type=int)
    parser.add_argument("--log-file", required=True)
    args = parser.parse_args()

    configure_logging(args.log_file)

    bot = BridgeBot(args.user_id)
    server = BridgeServer(bot, args.port)
    await server.start()

    bot_task = asyncio.create_task(bot.start(args.token))
    done, pending = await asyncio.wait(
        [bot_task, asyncio.create_task(bot.shutdown_event.wait())],
        return_when=asyncio.FIRST_COMPLETED,
    )

    for task in pending:
        task.cancel()

    if bot_task in done:
        try:
            await bot_task
        except Exception:
            logging.exception("Discord bot task crashed")
            raise


if __name__ == "__main__":
    asyncio.run(main())
