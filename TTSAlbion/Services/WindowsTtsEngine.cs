// TTSAlbion/Services/Tts/WindowsTtsEngine.cs

using System.IO;
using System.Speech.Synthesis;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Services.Tts;

/// <summary>
/// Windows SAPI implementation. No external dependencies.
/// For production, consider Azure or ElevenLabs behind the same interface.
/// </summary>
public sealed class WindowsTtsEngine : ITtsEngine, IDisposable
{
    // SpeechSynthesizer is NOT thread-safe — one instance per call or use lock
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly SpeechSynthesizer _synth = new();

    public async Task<byte[]> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            using var stream = new MemoryStream();
            _synth.SetOutputToWaveStream(stream);
            _synth.Speak(text);           // Sync — SAPI no tiene async nativo
            _synth.SetOutputToDefaultAudioDevice();
            return stream.ToArray();      // WAV completo, el sink hace el decode
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _synth.Dispose();
}