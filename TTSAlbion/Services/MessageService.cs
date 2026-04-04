// TTSAlbion/Services/MessageService.cs

using System.IO;
using TTSAlbion.Albion.Handler.Event.Model;
using TTSAlbion.Interfaces;
using TTSAlbion.Services.Audio;
using TTSAlbion.Services.Tts;

namespace TTSAlbion.Services;

/// <summary>
/// Orchestrates the pipeline: message → command detection → TTS → Discord.
/// Has no knowledge of audio formats, Discord API, or synthesis engines.
/// </summary>
public sealed class MessageService : IDisposable
{
    private readonly ICommandParser _commandParser;
    private readonly ITtsEngine _ttsEngine;
    private readonly IAudioSink _audioSink;
    private readonly IWavToPcmConverter _wavConverter;

    private string? _registeredUser;

    public MessageService(
        ICommandParser commandParser,
        ITtsEngine ttsEngine,
        IWavToPcmConverter wavConverter,
        IAudioSink audioSink)
    {
        _commandParser = commandParser;
        _ttsEngine     = ttsEngine;
        _wavConverter  = wavConverter;
        _audioSink     = audioSink;
    }

    public void RegisterUser(string username) => _registeredUser = username;

    public async Task RunCommandAsync(MessageModel message)
    {
        if (_registeredUser is null) return;
        if (!message.User.Equals(_registeredUser, StringComparison.OrdinalIgnoreCase)) return;
        if (!_commandParser.TryParse(message.Text, out var payload)) return;

        var wav = await _ttsEngine.SynthesizeAsync(payload);
        if (wav.Length == 0) return;
        
        // Abrir archivo como stream (lazy, eficiente)
        var pcm = _wavConverter.Convert(wav);
        await _audioSink.SendAsync(pcm);
    }

    public void Dispose()
    {
        if (_ttsEngine is IDisposable d) d.Dispose();
    }
}