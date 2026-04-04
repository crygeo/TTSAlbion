// TTSAlbion/Services/Audio/IDiscordAudioSink.cs
namespace TTSAlbion.Interfaces;

/// <summary>
/// Sends audio to a Discord voice channel.
/// Decoupled from TTS: accepts raw PCM, not text.
/// </summary>
public interface IDiscordAudioSink
{
    /// <summary>PCM 16-bit signed, 48kHz, mono.</summary>
    Task SendAsync(byte[] pcm, CancellationToken ct = default);
}