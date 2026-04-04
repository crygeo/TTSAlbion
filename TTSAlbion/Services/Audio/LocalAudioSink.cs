// Infrastructure.Audio

using System.IO;
using NAudio.Wave;
using TTSAlbion.Interfaces;

public sealed class LocalAudioSink : IAudioSink, IDisposable
{
    private readonly WaveOutEvent _output = new();
    private readonly BufferedWaveProvider _buffer;

    // Acepta el formato real de SAPI en lugar de hardcodear Discord
    public LocalAudioSink(WaveFormat? format = null)
    {
        var waveFormat = format ?? new WaveFormat(16000, 16, 1); // SAPI default
        _buffer = new BufferedWaveProvider(waveFormat)
        {
            DiscardOnBufferOverflow = true
        };
        _output.Init(_buffer);
        _output.Play();
    }

    public Task SendAsync(byte[] pcm, CancellationToken ct = default)
    {
        if (pcm is { Length: > 0 })
            _buffer.AddSamples(pcm, 0, pcm.Length);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _output.Stop();
        _output.Dispose();
    }
}

