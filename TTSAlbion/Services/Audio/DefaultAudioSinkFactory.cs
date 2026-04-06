using Discord;
using Discord.WebSocket;
using NAudio.Wave;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Services.Audio;

/// <summary>
/// Default implementation of <see cref="IAudioSinkFactory"/>.
/// Each <see cref="Create"/> call returns a fresh, independent sink instance.
/// Discord bot sinks manage their own <see cref="DiscordSocketClient"/> lifecycle.
/// </summary>
public sealed class DefaultAudioSinkFactory : IAudioSinkFactory
{
    private AudioSinkType _lastCreatedType;

    public async Task<IAudioSink> Create(AudioSinkType type)
    {
        return type switch
        {
            AudioSinkType.Local => new LocalAudioSink(),
            AudioSinkType.VirtualMic => new VirtualMicAudioSink(),
            AudioSinkType.DiscordBot => new DiscordAudioSink(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

}

