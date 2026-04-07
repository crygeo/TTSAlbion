using Discord;
using Discord.Audio;
using Discord.WebSocket;
using TTSAlbion.Infrastructure;
using TTSAlbion.Interfaces;
using TTSAlbion.Services.Audio;

namespace TTSAlbion.Services.Audio;

/// <summary>
/// Sink con ciclo de vida autónomo.
///
/// Estados: Uninitialized → Starting → Connected → Stopped
/// 
/// Re-keying DAVE:
///   IAudioClient.StreamDestroyed → _currentStream = null  (frames descartados silenciosamente)
///   IAudioClient.StreamCreated   → swap atómico bajo lock
///   El lock protege SOLO el puntero. WriteAsync corre fuera del lock.
///
/// Start() lanza InvalidOperationException si ya está Started/Connected.
/// SendAsync descarta silenciosamente si _currentStream es null
/// (reconexión en curso, pérdida < 20ms, aceptable).
/// </summary>
public sealed class DiscordAudioSink : IAudioSink, IAsyncDisposable
{
    private enum SinkState { Uninitialized, Starting, Connected, Stopped }

    private readonly SemaphoreSlim _streamLock  = new(1, 1);
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly IWavToPcmConverter _converter = new ResamplingWavToPcmConverter();

    private volatile SinkState    _state = SinkState.Uninitialized;
    private DiscordSocketClient?  _client;
    private IAudioClient?         _audioClient;
    private AudioOutStream?       _currentStream;

    private const int FrameSize = 3840; // 20 ms @ 48 kHz, 16-bit, stereo

    // ── ILifecycleAudioSink ──────────────────────────────────────────────────────

    /// <summary>
    /// Crea el cliente, hace login, se une al canal de voz.
    /// Retorna info de conexión para que el ViewModel actualice la UI.
    /// Lanza InvalidOperationException si ya está iniciado.
    /// </summary>
    public async Task<DiscordConnectionInfo> StartAsync(
        DiscordBotConfig config,
        CancellationToken ct = default)
    {
        if (_state == SinkState.Starting ||  _state == SinkState.Connected)
            throw new InvalidOperationException(
                $"El sink ya está en estado {_state}. Llama StopAsync() antes de reiniciar.");

        _state = SinkState.Starting;

        try
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents           = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
                DefaultRetryMode         = RetryMode.AlwaysRetry,
                EnableVoiceDaveEncryption = true,
            });

            // Ready gate
            var readyTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _client.Ready += () => { readyTcs.TrySetResult(); return Task.CompletedTask; };

            await _client.LoginAsync(TokenType.Bot, config.Token).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);

            var readyTimeout = Task.Delay(TimeSpan.FromSeconds(15), ct);
            if (await Task.WhenAny(readyTcs.Task, readyTimeout).ConfigureAwait(false) == readyTimeout)
                throw new TimeoutException("El bot no recibió el evento Ready en 15 segundos.");

            // Unirse al canal de voz
            var guild   = _client.GetGuild(config.GuildId)
                          ?? throw new InvalidOperationException($"Guild {config.GuildId} no encontrado.");
            var channel = guild.GetVoiceChannel(config.VoiceChannelId)
                          ?? throw new InvalidOperationException($"Canal {config.VoiceChannelId} no encontrado.");

            _audioClient = await Task.Run(
                () => channel.ConnectAsync(selfDeaf: true, selfMute: false, disconnect: false), ct)
                .ConfigureAwait(false);

            // Suscribirse a eventos de re-keying
            _audioClient.StreamCreated   += OnStreamCreated;
            _audioClient.StreamDestroyed += OnStreamDestroyed;

            // Stream inicial
            _currentStream = _audioClient.CreatePCMStream(AudioApplication.Voice, bufferMillis: 200);

            _state = SinkState.Connected;

            return new DiscordConnectionInfo(guild.Name, channel.Name);
        }
        catch
        {
            _state = SinkState.Uninitialized; // permite reintentar
            await CleanupClientAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_state == SinkState.Stopped || _state == SinkState.Uninitialized) return;

        _state = SinkState.Stopped;

        await _streamLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_currentStream is not null)
            {
                await _currentStream.FlushAsync(ct).ConfigureAwait(false);
                await _currentStream.DisposeAsync().ConfigureAwait(false);
                _currentStream = null;
            }
        }
        finally
        {
            _streamLock.Release();
        }

        _state = SinkState.Uninitialized;
        await CleanupClientAsync().ConfigureAwait(false);
    }

    // ── IAudioSink ───────────────────────────────────────────────────────────────

    public async Task SendAsync(byte[] pcm, CancellationToken ct = default)
    {
        if (_state != SinkState.Connected) 
            throw new InvalidOperationException($"El sink no está conectado. Estado actual: {_state}.");

        pcm = _converter.Convert(pcm);
        if (pcm.Length == 0) return;

        // Leer el puntero bajo lock fino (O(1))
        AudioOutStream? stream;
        await _streamLock.WaitAsync(ct).ConfigureAwait(false);
        try   { stream = _currentStream; }
        finally { _streamLock.Release(); }

        // null = re-keying en curso; descartar frame silenciosamente
        if (stream is null) return;

        // Escritura fuera del lock — el swap en OnStreamCreated es transparente
        int offset = 0;
        while (offset < pcm.Length)
        {
            int chunk = Math.Min(FrameSize, pcm.Length - offset);
            await stream.WriteAsync(pcm.AsMemory(offset, chunk), ct).ConfigureAwait(false);
            offset += chunk;
        }

        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    // ── Re-keying handlers ────────────────────────────────────────────────────────

    private Task OnStreamDestroyed(ulong userId)
    {
        // Setear null atómicamente — SendAsync verá null en el próximo frame y descartará
        _streamLock.Wait();
        try   { _currentStream = null; }
        finally { _streamLock.Release(); }

        return Task.CompletedTask;
    }

    private async Task OnStreamCreated(ulong userId, AudioInStream inStream)
    {
        if (_audioClient is null) return;

        // Crear nuevo stream de salida con las nuevas claves
        var newStream = _audioClient.CreatePCMStream(AudioApplication.Voice, bufferMillis: 200);

        AudioOutStream? oldStream;

        await _streamLock.WaitAsync().ConfigureAwait(false);
        try
        {
            oldStream      = _currentStream;
            _currentStream = newStream;       // swap atómico
        }
        finally
        {
            _streamLock.Release();
        }

        // Disponer el stream viejo fuera del lock
        if (oldStream is not null)
        {
            try   { await oldStream.DisposeAsync().ConfigureAwait(false); }
            catch { /* ignorar errores de dispose en stream invalidado */ }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task CleanupClientAsync()
    {
        if (_audioClient is not null)
        {
            _audioClient.StreamCreated   -= OnStreamCreated;
            _audioClient.StreamDestroyed -= OnStreamDestroyed;
            _audioClient.Dispose();
            _audioClient = null;
        }

        if (_client is not null)
        {
            try
            {
                await _client.StopAsync().ConfigureAwait(false);
                await _client.LogoutAsync().ConfigureAwait(false);
            }
            catch { /* best-effort */ }

            _client.Dispose();
            _client = null;
        }
    }

    // ── IAsyncDisposable ─────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _streamLock.Dispose();
        _connectLock.Dispose();
    }
}

public sealed record DiscordConnectionInfo(
    string GuildName,
    string ChannelName);
    
/// <summary>
/// Configuration payload required when <see cref="AudioSinkType.DiscordBot"/> is selected.
/// Kept as a value-type record so it can be passed around cheaply and compared by value.
/// </summary>
public sealed record DiscordBotConfig(string Token, ulong GuildId, ulong VoiceChannelId);