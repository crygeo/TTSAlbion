// TTSAlbion/Infrastructure/NaudioDeviceDetector.cs
using NAudio.Wave;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Infrastructure;

/// <summary>
/// NAudio-based implementation.
/// Iterates WaveOut devices once per call — cheap enough for startup
/// and on-demand checks. No caching needed: device changes are rare
/// and the cost is negligible (~microseconds).
/// </summary>
public sealed class NaudioDeviceDetector : IAudioDeviceDetector
{
    public bool IsDeviceAvailable(string deviceName)
    {
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            if (caps.ProductName.Contains(deviceName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}