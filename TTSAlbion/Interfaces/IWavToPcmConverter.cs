// TTSAlbion/Services/Audio/IWavToPcmConverter.cs

using System.IO;
using NAudio.Wave;

namespace TTSAlbion.Services.Audio;

public interface IWavToPcmConverter
{
    /// <summary>Strips WAV header, returns raw 16-bit PCM.</summary>
    byte[] Convert(byte[] wav);
}

// TTSAlbion/Services/Audio/WavToPcmConverter.cs
[Obsolete("Use ConvertToFrames for streaming conversion instead.")]
public sealed class WavToPcmConverter : IWavToPcmConverter
{
    private const int WavHeaderSize = 44; // estándar PCM WAV

    private int _fistConvert;
    public WavToPcmConverter(int primerConvert = 1)
    {
        _fistConvert = primerConvert;
    }

    public byte[] Convert(byte[] wav)
    {
        byte[] converter; 
        
        switch (_fistConvert)
        {
            case 0: converter = wav; break;
            case 1: converter = Convert1(wav); break;
            case 2: converter = Convert2(wav); break;
            default: throw new InvalidOperationException("Invalid conversion method.");
        }
        return converter;
    }

    private byte[] Convert1(byte[] wav)
    {
        if (wav.Length <= WavHeaderSize)
            return Array.Empty<byte>();

        var pcm = new byte[wav.Length - WavHeaderSize];
        Buffer.BlockCopy(wav, WavHeaderSize, pcm, 0, pcm.Length);
        return pcm;
    }

    private byte[] Convert2(byte[] wavBytes)
    {
        using var ms = new MemoryStream(wavBytes);
        using var reader = new WaveFileReader(ms);

        var targetFormat = new WaveFormat(48000, 16, 2);

        using var resampler = new MediaFoundationResampler(reader, targetFormat);
        using var pcmStream = new MemoryStream();

        var buffer = new byte[8192];
        int read;

        while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
        {
            pcmStream.Write(buffer, 0, read);
        }

        return pcmStream.ToArray();
    }
    
    private const int FrameSize = 3840;

    public static async IAsyncEnumerable<byte[]> ConvertToFrames(byte[] wav)
    {
        using var ms = new MemoryStream(wav);
        using var reader = new WaveFileReader(ms);

        var targetFormat = new WaveFormat(48000, 16, 2);
        using var resampler = new MediaFoundationResampler(reader, targetFormat);

        var buffer = new byte[FrameSize];

        int read;
        while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (read < FrameSize)
            {
                // padding final
                Array.Clear(buffer, read, FrameSize - read);
            }

            yield return buffer.ToArray();
            await Task.Yield(); // evita bloqueo
        }
    }
}