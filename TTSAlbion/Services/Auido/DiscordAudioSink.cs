// TTSAlbion/Services/Audio/DiscordAudioSink.cs
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Services.Audio;

/// <summary>
/// Streams PCM audio to a Discord voice channel via Discord.Net.
/// Handles reconnection and channel acquisition internally.
/// </summary>
public sealed class DiscordAudioSink : IAudioSink, IAsyncDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly ulong _guildId;
    private readonly ulong _voiceChannelId;

    private IAudioClient? _audioClient;
    private AudioOutStream? _outStream;

    public DiscordAudioSink(DiscordSocketClient client, ulong guildId, ulong voiceChannelId)
    {
        _client = client;
        _guildId = guildId;
        _voiceChannelId = voiceChannelId;
    }

    public async Task SendAsync(byte[] pcm, CancellationToken ct = default)
    {
        var stream = await GetOrConnectAsync(ct);
        await stream.WriteAsync(pcm, ct);
        await stream.FlushAsync(ct);
    }

    private async Task<AudioOutStream> GetOrConnectAsync(CancellationToken ct)
    {
        if (_outStream is not null && _audioClient?.ConnectionState == ConnectionState.Connected)
            return _outStream;

        var guild = _client.GetGuild(_guildId);
        var channel = guild.GetVoiceChannel(_voiceChannelId);
        _audioClient = await channel.ConnectAsync();
        _outStream = _audioClient.CreatePCMStream(AudioApplication.Voice);
        return _outStream;
    }

    public async ValueTask DisposeAsync()
    {
        if (_outStream is not null) await _outStream.DisposeAsync();
        _audioClient?.Dispose();
    }
}