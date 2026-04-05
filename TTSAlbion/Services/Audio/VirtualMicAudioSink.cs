using NAudio.Wave;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Services.Audio;

public sealed class VirtualMicAudioSink : IAudioSink, IDisposable
{
    private readonly WaveOutEvent _output;
    private readonly BufferedWaveProvider _buffer;
    private IWavToPcmConverter _converter = new WavToPcmConverter(1);

    public VirtualMicAudioSink(string deviceName = "CABLE Input")
    {
        int deviceNumber = FindDeviceByName(deviceName);

        _buffer = new BufferedWaveProvider(new WaveFormat(16000, 16, 1))
        {
            DiscardOnBufferOverflow = true
        };

        _output = new WaveOutEvent { DeviceNumber = deviceNumber };
        _output.Init(_buffer);
        _output.Play();
    }

    public Task SendAsync(byte[] pcm, CancellationToken ct = default)
    {
        pcm = _converter.Convert(pcm);
        if (pcm is { Length: > 0 })
            _buffer.AddSamples(pcm, 0, pcm.Length);
        return Task.CompletedTask;
    }

    private static int FindDeviceByName(string name)
    {
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            if (caps.ProductName.Contains(name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new InvalidOperationException(
            $"Dispositivo de audio virtual '{name}' no encontrado. " +
            $"Instala VB-Audio Virtual Cable y selecciona 'CABLE Input' como micrófono en Discord.");
    }

    public void Dispose()
    {
        _output.Stop();
        _output.Dispose();
    }
}