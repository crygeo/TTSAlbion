// TTSAlbion/Services/Audio/SinkAvailabilityService.cs
using TTSAlbion.Interfaces;

namespace TTSAlbion.Services.Audio;

/// <summary>
/// Single source of truth for sink availability.
/// Each call to GetAvailability() re-evaluates — no stale state.
/// The ViewModel calls this once at startup and on explicit refresh.
/// </summary>
public sealed class SinkAvailabilityService : ISinkAvailabilityService
{
    private const string VirtualCableDeviceName = "CABLE Input";

    private readonly IAudioDeviceDetector _detector;

    public SinkAvailabilityService(IAudioDeviceDetector detector)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
    }

    public IReadOnlyList<SinkAvailability> GetAvailability()
    {
        var virtualMicAvailable = _detector.IsDeviceAvailable(VirtualCableDeviceName);
        var hasPython = DiscordAudioSink.TryResolvePythonExecutable(out _, out var pythonReason);
        var hasPythonScript = DiscordAudioSink.TryResolvePythonScriptPath(out _, out var scriptReason);
        var discordBotAvailable = hasPython && hasPythonScript;
        var discordBotReason = !hasPython
            ? pythonReason
            : !hasPythonScript
                ? scriptReason
                : null;

        return new[]
        {
            new SinkAvailability(AudioSinkType.Local,      IsAvailable: true),
            new SinkAvailability(AudioSinkType.VirtualMic, virtualMicAvailable,
                virtualMicAvailable ? null :
                    "Requiere VB-Audio Virtual Cable. Descárgalo en vb-audio.com"),
            new SinkAvailability(AudioSinkType.DiscordBot, discordBotAvailable,
                discordBotAvailable ? null : discordBotReason),
        };
    }

    public bool IsAvailable(AudioSinkType type)
        => GetAvailability().FirstOrDefault(x => x.Type == type)?.IsAvailable ?? false;

    public string? GetUnavailableReason(AudioSinkType type)
        => GetAvailability().FirstOrDefault(x => x.Type == type)?.UnavailableReason;
}
