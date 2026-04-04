using System.IO;
using NAudio.Wave;
using TTSAlbion.Services.Audio;

namespace TTSAlbion.Infrastructure;

// IWavToPcmConverter.cs — sin cambios en el contrato público

// ResamplingWavToPcmConverter.cs — única implementación correcta
public sealed class ResamplingWavToPcmConverter : IWavToPcmConverter
{
    private readonly WaveFormat _targetFormat;

    public ResamplingWavToPcmConverter(int sampleRate = 48000, int bitDepth = 16, int channels = 2)
    {
        _targetFormat = new WaveFormat(sampleRate, bitDepth, channels);
    }

    public byte[] Convert(byte[] wav)
    {
        if (wav is null or { Length: 0 })
            return Array.Empty<byte>();

        using var ms = new MemoryStream(wav);
        using var reader = new WaveFileReader(ms);

        // Si el formato ya coincide, evita el resampleo innecesario
        if (reader.WaveFormat.SampleRate == _targetFormat.SampleRate &&
            reader.WaveFormat.Channels   == _targetFormat.Channels    &&
            reader.WaveFormat.BitsPerSample == _targetFormat.BitsPerSample)
        {
            using var pcmOut = new MemoryStream();
            reader.CopyTo(pcmOut);
            return pcmOut.ToArray();
        }

        using var resampler = new MediaFoundationResampler(reader, _targetFormat)
        {
            ResamplerQuality = 60 // 1–60; 60 = máxima calidad, razonable para TTS
        };

        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0)
            output.Write(buffer, 0, read);

        return output.ToArray();
    }
}