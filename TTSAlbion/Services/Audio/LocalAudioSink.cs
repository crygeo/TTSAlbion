// Infrastructure.Audio

using System.IO;
using NAudio.Wave;
using TTSAlbion.Interfaces;

public sealed class LocalAudioSink : IAudioSink, IDisposable
{
    private readonly WaveOutEvent _output;
    private readonly BufferedWaveProvider _buffer;

    public LocalAudioSink()
    {
        var format = new WaveFormat(48000, 16, 2); // Discord-compatible PCM

        _buffer = new BufferedWaveProvider(format)
        {
            DiscardOnBufferOverflow = true
        };

        _output = new WaveOutEvent();
        _output.Init(_buffer);
        _output.Play();
    }

    public Task SendAsync(byte[] pcm, CancellationToken ct)
    {
        if (pcm == null || pcm.Length == 0)
            return Task.CompletedTask;
        
        _buffer.AddSamples(pcm, 0, pcm.Length);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _output?.Stop();
        _output?.Dispose();
    }
    
    
}

