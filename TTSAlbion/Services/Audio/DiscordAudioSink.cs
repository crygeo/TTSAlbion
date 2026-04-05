using Discord;
using Discord.Audio;
using Discord.WebSocket;
using TTSAlbion.Infrastructure;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Services.Audio;

/// <summary>
/// Sends PCM audio to a Discord voice channel.
/// Owns the <see cref="DiscordSocketClient"/> it was given (or creates one via the factory).
/// Connection is lazy: established on the first <see cref="SendAsync"/> call.
/// 
/// Design notes:
/// - Thread safety via SemaphoreSlim on the connection path (write path is single-producer).
/// - Token login and channel join run on a background thread to avoid WPF dispatcher deadlocks.
/// - Implements IAsyncDisposable; callers should await DisposeAsync when switching sinks.
/// </summary>
public sealed class DiscordAudioSink : IAudioSink, IAsyncDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly ulong _guildId;
    private readonly ulong _voiceChannelId;
    private readonly string? _token;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly IWavToPcmConverter _converter = new ResamplingWavToPcmConverter();

    private IAudioClient? _audioClient;
    private AudioOutStream? _outStream;
    private bool _loggedIn;

    private const int FrameSize = 3840; // 20 ms @ 48 kHz, 16-bit, stereo

    // ── Constructors ────────────────────────────────────────────────────────────

    /// <summary>
    /// Use when the caller manages <paramref name="client"/> login externally
    /// (legacy path kept for backward compatibility).
    /// </summary>
    public DiscordAudioSink(DiscordSocketClient client, ulong guildId, ulong voiceChannelId)
    {
        _client = client;
        _guildId = guildId;
        _voiceChannelId = voiceChannelId;

        _ = GetOrConnectAsync(CancellationToken.None);
    }

    

    // ── IAudioSink ───────────────────────────────────────────────────────────────

    public async Task SendAsync(byte[] pcm, CancellationToken ct = default)
    {
        pcm = _converter.Convert(pcm);
        
        var stream = await GetOrConnectAsync(ct).ConfigureAwait(false);

        int offset = 0;
        while (offset < pcm.Length)
        {
            int remaining = Math.Min(FrameSize, pcm.Length - offset);
            await stream.WriteAsync(pcm.AsMemory(offset, remaining), ct).ConfigureAwait(false);
            offset += remaining;
        }

        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    // ── Connection management ────────────────────────────────────────────────────

    private async Task<AudioOutStream> GetOrConnectAsync(CancellationToken ct)
    {
        // Fast path — already connected
        if (_outStream is not null && _audioClient?.ConnectionState == ConnectionState.Connected)
            return _outStream;

        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check inside lock
            if (_outStream is not null && _audioClient?.ConnectionState == ConnectionState.Connected)
                return _outStream;

            var guild = _client.GetGuild(_guildId)
                        ?? throw new InvalidOperationException($"Guild {_guildId} not found.");
            var channel = guild.GetVoiceChannel(_voiceChannelId)
                          ?? throw new InvalidOperationException($"Voice channel {_voiceChannelId} not found.");

            // ConnectAsync must not run on the WPF dispatcher thread.
            _audioClient = await Task.Run(() => channel.ConnectAsync(selfDeaf: true, selfMute: false, disconnect: false), ct)
                                     .ConfigureAwait(false);

            _outStream = _audioClient.CreatePCMStream(AudioApplication.Voice, bufferMillis: 200);
            return _outStream;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    // ── IAsyncDisposable ─────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_outStream is not null)
        {
            await _outStream.FlushAsync().ConfigureAwait(false);
            await _outStream.DisposeAsync().ConfigureAwait(false);
        }

        _audioClient?.Dispose();

        if (_loggedIn)
        {
            await _client.StopAsync().ConfigureAwait(false);
            await _client.LogoutAsync().ConfigureAwait(false);
        }

        _client.Dispose();
        _connectLock.Dispose();
    }
}