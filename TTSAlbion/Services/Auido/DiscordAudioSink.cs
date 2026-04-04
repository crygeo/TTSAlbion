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
    
    private bool _isConnecting;

    public DiscordAudioSink(DiscordSocketClient client, ulong guildId, ulong voiceChannelId)
    {
        _client = client;
        _guildId = guildId;
        _voiceChannelId = voiceChannelId;
        
    }

    public async Task SendAsync(byte[] pcm, CancellationToken ct = default)
    {
        var pcmnew = WavToPcmConverter.ConvertToFrames(pcm);
        
        await SendAsync(pcmnew, ct);
    }
    
    public async Task SendAsync(IAsyncEnumerable<byte[]> frames, CancellationToken ct)
    {
        var stream = await GetOrConnectAsync(ct);

        await foreach (var frame in frames.WithCancellation(ct))
        {
            await stream.WriteAsync(frame, ct);
            await Task.Delay(20, ct); // Discord recomienda enviar cada 20ms para evitar buffer underruns
        }
    }


    private async Task<AudioOutStream> GetOrConnectAsync(CancellationToken ct)
    {
        if (_outStream is not null && _audioClient?.ConnectionState == ConnectionState.Connected)
            return _outStream;

        var guild = _client.GetGuild(_guildId)
                    ?? throw new InvalidOperationException("Guild no encontrado");

        var channel = guild.GetVoiceChannel(_voiceChannelId)
                      ?? throw new InvalidOperationException("Canal de voz no encontrado");

        // ✅ Ejecutar ConnectAsync sin bloquear el UI thread
        _audioClient = await Task.Run(() => channel.ConnectAsync(selfDeaf: true), ct)
            .ConfigureAwait(false);

        _outStream = _audioClient.CreatePCMStream(AudioApplication.Voice);

        return _outStream;
    }

    public async ValueTask DisposeAsync()
    {
        if (_outStream is not null) await _outStream.DisposeAsync();
        _audioClient?.Dispose();
    }
}