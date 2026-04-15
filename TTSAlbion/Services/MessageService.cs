using TTSAlbion.Albion.Handler.Event.Model;
using TTSAlbion.Interfaces;
using TTSAlbion.Services.Audio;
using System.Threading.Channels;

namespace TTSAlbion.Services;

/// <summary>
/// Orchestrates: command detection → TTS synthesis → audio sink.
///
/// The service is deliberately agnostic about which chat event produced
/// the text — callers normalise to (user, text) before invoking it.
///
/// Design notes:
/// - <see cref="UpdateSink"/> replaces the active sink atomically using a lock
///   so in-flight sends are not interrupted mid-frame.
/// - The sink is disposed on replacement and on <see cref="Dispose"/>.
/// - <see cref="_registeredUser"/> is set externally by the ViewModel;
///   null means "do not process any messages".
/// </summary>
public sealed class MessageService : IDisposable
{
    private readonly ICommandParser _commandParser;
    private readonly ITtsEngine _ttsEngine;
    private readonly SemaphoreSlim _pipelineLock = new(1, 1);
    private readonly Channel<string> _queue;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _worker;

    public IAudioSink AudioSink {get ; private set;}
    private readonly object _sinkLock = new();

    private volatile string? _registeredUser;

    public MessageService(ICommandParser commandParser, ITtsEngine ttsEngine, IAudioSink audioSink)
    {
        _commandParser = commandParser ?? throw new ArgumentNullException(nameof(commandParser));
        _ttsEngine = ttsEngine ?? throw new ArgumentNullException(nameof(ttsEngine));
        AudioSink = audioSink ?? throw new ArgumentNullException(nameof(audioSink));
        _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _worker = Task.Run(ProcessQueueAsync);
    }

    // ── Configuration ────────────────────────────────────────────────────────────

    public void RegisterUser(string? username) => _registeredUser = username;

    /// <summary>
    /// Replaces the active audio sink.
    /// The previous sink is disposed after the swap so any in-flight send can finish.
    /// </summary>
    public void UpdateSink(IAudioSink newSink)
    {
        ArgumentNullException.ThrowIfNull(newSink);

        IAudioSink old;
        _pipelineLock.Wait();
        try
        {
            lock (_sinkLock)
            {
                old = AudioSink;
                AudioSink = newSink;
            }
        }
        finally
        {
            _pipelineLock.Release();
        }

        if (old is IDisposable d) d.Dispose();
    }

    // ── Pipeline entry points ─────────────────────────────────────────────────────

    // ================================
    // Adapter (evita duplicación)
    // ================================
    public Task RunCommandAsync(MessageModel message)
    {
        return RunCommandAsync(message.User, message.Message);
    }
    
    public Task RunCommandAsync(ChatSayModel message)
    {
        return ExecuteAsync(message.Message);
    }
    
    // ================================
    // Pipeline unificado
    // ================================
    public async Task RunCommandAsync(string user, string text)
    {
        // Parseo
        if (!TryGetCommand(text, out var payload)) return;

        // Ejecución
        await ExecuteAsync(payload);
    }

    // ================================
    // Parser de comando
    // ================================
    private bool TryGetCommand(string text, out string payload)
    {
        return _commandParser.TryParse(text, out payload);
    }

    // ================================
    // Ejecución pura (single responsibility)
    // ================================
    public Task ExecuteAsync(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return Task.CompletedTask;

        Console.WriteLine($"[MessageService] Enqueued payload len={payload.Length}");
        return _queue.Writer.WriteAsync(payload, _shutdown.Token).AsTask();
    }

    // ── Internal pipeline ─────────────────────────────────────────────────────────

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var payload in _queue.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
            {
                try
                {
                    Console.WriteLine($"[MessageService] Dequeued payload len={payload.Length}");
                    await SynthesizeAndSendAsync(payload).ConfigureAwait(false);
                    Console.WriteLine($"[MessageService] Completed payload len={payload.Length}");
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MessageService] Queue item failed: {ex}");
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // shutdown normal
        }
    }

    private async Task SynthesizeAndSendAsync(string payload)
    {
        var acquired = false;
        try
        {
            await _pipelineLock.WaitAsync(_shutdown.Token).ConfigureAwait(false);
            acquired = true;

            Console.WriteLine($"[MessageService] Synthesizing payload len={payload.Length}");
            var wav = await _ttsEngine.SynthesizeAsync(payload, _shutdown.Token).ConfigureAwait(false);
            Console.WriteLine($"[MessageService] Synthesized bytes={wav.Length}");
            if (wav.Length == 0) return;

            IAudioSink sink;
            lock (_sinkLock)
            {
                sink = AudioSink;
            }

            Console.WriteLine($"[MessageService] Sending bytes={wav.Length} sink={sink.GetType().Name}");
            await sink.SendAsync(wav, _shutdown.Token).ConfigureAwait(false);
            Console.WriteLine($"[MessageService] Sent bytes={wav.Length} sink={sink.GetType().Name}");
        }
        finally
        {
            if (acquired)
                _pipelineLock.Release();
        }
    }

    // ── IDisposable ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        _shutdown.Cancel();
        try { _worker.Wait(TimeSpan.FromSeconds(2)); }
        catch { /* best-effort */ }
        _shutdown.Dispose();
        _pipelineLock.Dispose();
        if (_ttsEngine is IDisposable td) td.Dispose();
        if (AudioSink is IDisposable sd) sd.Dispose();
    }
}
