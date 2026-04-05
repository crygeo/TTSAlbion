using TTSAlbion.Albion.Handler.Event.Model;
using TTSAlbion.Interfaces;
using TTSAlbion.Services.Audio;

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

    private IAudioSink _audioSink;
    private readonly object _sinkLock = new();

    private volatile string? _registeredUser;

    public MessageService(ICommandParser commandParser, ITtsEngine ttsEngine, IAudioSink audioSink)
    {
        _commandParser = commandParser ?? throw new ArgumentNullException(nameof(commandParser));
        _ttsEngine = ttsEngine ?? throw new ArgumentNullException(nameof(ttsEngine));
        _audioSink = audioSink ?? throw new ArgumentNullException(nameof(audioSink));
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
        lock (_sinkLock)
        {
            old = _audioSink;
            _audioSink = newSink;
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
        return SynthesizeAndSendAsync(payload);
    }

    // ── Internal pipeline ─────────────────────────────────────────────────────────

    private async Task SynthesizeAndSendAsync(string payload)
    {
        var wav = await _ttsEngine.SynthesizeAsync(payload).ConfigureAwait(false);
        if (wav.Length == 0) return;

        await _audioSink.SendAsync(wav);
    }

    // ── IDisposable ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_ttsEngine is IDisposable td) td.Dispose();
        if (_audioSink is IDisposable sd) sd.Dispose();
    }
}