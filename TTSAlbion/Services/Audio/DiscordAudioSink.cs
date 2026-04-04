using Discord;
using Discord.Audio;
using Discord.WebSocket;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Services.Audio;

public sealed class DiscordAudioSink : IAudioSink, IAsyncDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly ulong _guildId;
    private readonly ulong _voiceChannelId;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private IAudioClient? _audioClient;
    private AudioOutStream? _outStream;

    private const int FrameSize = 3840; // 20ms a 48kHz, 16-bit, stereo

    public DiscordAudioSink(DiscordSocketClient client, ulong guildId, ulong voiceChannelId)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _guildId = guildId;
        _voiceChannelId = voiceChannelId;
    }

    /// <summary>
    /// Envía un buffer PCM completo (WAV convertido) al canal de voz,
    /// fragmentando automáticamente en frames de 20ms.
    /// </summary>
    // Versión más correcta: deja que el buffer de Discord maneje el timing
    public async Task SendAsync(byte[] pcm, CancellationToken ct = default)
    {
        var stream = await GetOrConnectAsync(ct);
    
        // Discord.Net AudioOutStream ya tiene buffering interno de 200ms (bufferMillis)
        // WriteAsync es non-blocking hasta que el buffer se llena — no necesitas Delay manual
        int offset = 0;
        while (offset < pcm.Length)
        {
            int remaining = Math.Min(FrameSize, pcm.Length - offset);
            await stream.WriteAsync(pcm.AsMemory(offset, remaining), ct);
            offset += remaining;
        }
    
        // Flush explícito al terminar el utterance
        await stream.FlushAsync(ct);
    }

    /// <summary>
    /// Obtiene o crea la conexión al canal de voz.
    /// </summary>
    private async Task<AudioOutStream> GetOrConnectAsync(CancellationToken ct)
    {
        // Fast path    
        if (_outStream is not null && _audioClient?.ConnectionState == ConnectionState.Connected)
            return _outStream;

        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check dentro del lock
            if (_outStream is not null && _audioClient?.ConnectionState == ConnectionState.Connected)
                return _outStream;

            var guild = _client.GetGuild(_guildId)
                        ?? throw new InvalidOperationException($"Guild {_guildId} no encontrado.");
            var channel = guild.GetVoiceChannel(_voiceChannelId)
                          ?? throw new InvalidOperationException($"Canal {_voiceChannelId} no encontrado.");

            // ✅ Crucial: ejecutar ConnectAsync en hilo de background para no bloquear WPF
            _audioClient = await Task.Run(() => channel.ConnectAsync(selfDeaf: true, selfMute: false), ct)
                                     .ConfigureAwait(false);

            _outStream = _audioClient.CreatePCMStream(AudioApplication.Voice, bufferMillis: 200);

            return _outStream;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Fragmenta PCM arbitrario en frames de 3840 bytes.
    /// Último frame se completa con ceros si no llena 3840.
    /// </summary>
    public static async IAsyncEnumerable<byte[]> ConvertToFrames(byte[] pcm)
    {
        int offset = 0;
        while (offset < pcm.Length)
        {
            var frame = new byte[FrameSize];
            int remaining = Math.Min(FrameSize, pcm.Length - offset);
            Buffer.BlockCopy(pcm, offset, frame, 0, remaining);
            offset += remaining;
            yield return frame;
            await Task.Yield(); // libera hilo para UI / Discord events
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_outStream is not null)
        {
            await _outStream.FlushAsync().ConfigureAwait(false);
            await _outStream.DisposeAsync().ConfigureAwait(false);
        }
        _audioClient?.Dispose();
        _connectLock.Dispose();
    }
}