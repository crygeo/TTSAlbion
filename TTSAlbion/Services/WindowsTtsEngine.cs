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
            var installedVoices = _synth.GetInstalledVoices()
                .Where(v => v.Enabled)
                .Select(v => v.VoiceInfo.Name)
                .ToArray();

            Console.WriteLine($"[WindowsTtsEngine] Installed voices count={installedVoices.Length}");
            if (installedVoices.Length > 0)
                Console.WriteLine($"[WindowsTtsEngine] Voices={string.Join(", ", installedVoices)}");

            if (installedVoices.Length == 0)
                throw new InvalidOperationException(
                    "No hay voces SAPI instaladas o habilitadas en Windows. System.Speech no puede sintetizar audio en este equipo.");

            using var stream = new MemoryStream();
            _synth.SetOutputToWaveStream(stream);
            _synth.Speak(text);           // Sync — SAPI no tiene async nativo
            _synth.SetOutputToDefaultAudioDevice();

            var wav = stream.ToArray();
            Console.WriteLine($"[WindowsTtsEngine] Synthesized wav bytes={wav.Length}");

            if (wav.Length == 0)
                throw new InvalidOperationException(
                    "System.Speech no generó audio. Revisa si Windows tiene voces instaladas y servicios de Speech habilitados.");

            return wav;      // WAV completo, el sink hace el decode
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Fallo el motor TTS de Windows (System.Speech). Es posible que este Windows no tenga los componentes de voz necesarios. {ex.Message}",
                ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _synth.Dispose();
}
