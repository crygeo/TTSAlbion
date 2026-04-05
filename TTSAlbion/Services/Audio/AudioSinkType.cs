using TTSAlbion.Interfaces;

namespace TTSAlbion.Services.Audio;

/// <summary>
/// Enumeration of the available audio output backends.
/// Exposed to the ViewModel so the UI can drive selection without
/// depending on concrete sink types.
/// </summary>
public enum AudioSinkType
{
    Local,
    VirtualMic,
    DiscordBot
}

/// <summary>
/// Creates and returns an <see cref="IAudioSink"/> for the given backend.
/// Centralises construction so neither the ViewModel nor App.xaml.cs
/// needs to reference concrete sink types.
/// </summary>
public interface IAudioSinkFactory
{
    /// <summary>
    /// Build a sink for the requested backend.
    /// The factory owns the lifetime of any shared resources it creates.
    /// Callers are responsible for disposing the returned instance when it
    /// is replaced or the application exits.
    /// </summary>
    Task<IAudioSink> Create(AudioSinkType type, DiscordBotConfig? botConfig = null);
}

/// <summary>
/// Configuration payload required when <see cref="AudioSinkType.DiscordBot"/> is selected.
/// Kept as a value-type record so it can be passed around cheaply and compared by value.
/// </summary>
public sealed record DiscordBotConfig(string Token, ulong GuildId, ulong VoiceChannelId);