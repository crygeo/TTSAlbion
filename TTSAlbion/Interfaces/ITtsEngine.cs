// TTSAlbion/Services/Tts/ITtsEngine.cs
namespace TTSAlbion.Interfaces;

/// <summary>
/// Synthesizes text to raw audio.
/// Implementations can wrap Windows SAPI, Azure Cognitive Services, etc.
/// </summary>
public interface ITtsEngine
{
    /// <summary>
    /// Returns PCM audio (16-bit, 48kHz, mono) ready for Discord encoding.
    /// Returns empty array if synthesis fails.
    /// </summary>
    Task<byte[]> SynthesizeAsync(string text, CancellationToken ct = default);
}