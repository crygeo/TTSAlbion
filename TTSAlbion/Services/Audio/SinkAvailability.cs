// TTSAlbion/Services/Audio/SinkAvailability.cs
namespace TTSAlbion.Services.Audio;

/// <summary>
/// Immutable descriptor of a sink's runtime availability.
/// Passed to the ViewModel so it never needs to know about device names.
/// </summary>
public sealed record SinkAvailability(AudioSinkType Type, bool IsAvailable, string? UnavailableReason = null);

// TTSAlbion/Interfaces/ISinkAvailabilityService.cs


public interface ISinkAvailabilityService
{
    IReadOnlyList<SinkAvailability> GetAvailability();
    bool IsAvailable(AudioSinkType type);
    string? GetUnavailableReason(AudioSinkType type);
}