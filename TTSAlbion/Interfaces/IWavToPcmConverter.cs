// TTSAlbion/Services/Audio/IWavToPcmConverter.cs
namespace TTSAlbion.Services.Audio;

public interface IWavToPcmConverter
{
    /// <summary>Strips WAV header, returns raw 16-bit PCM.</summary>
    byte[] Convert(byte[] wav);
}

// TTSAlbion/Services/Audio/WavToPcmConverter.cs
public sealed class WavToPcmConverter : IWavToPcmConverter
{
    private const int WavHeaderSize = 44; // estándar PCM WAV

    public byte[] Convert(byte[] wav)
    {
        if (wav.Length <= WavHeaderSize)
            return Array.Empty<byte>();

        var pcm = new byte[wav.Length - WavHeaderSize];
        Buffer.BlockCopy(wav, WavHeaderSize, pcm, 0, pcm.Length);
        return pcm;
    }
}