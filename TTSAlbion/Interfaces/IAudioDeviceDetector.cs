// TTSAlbion/Interfaces/IAudioDeviceDetector.cs
namespace TTSAlbion.Interfaces;

/// <summary>
/// Detects availability of named audio output devices.
/// Decoupled from any specific sink implementation.
/// </summary>
public interface IAudioDeviceDetector
{
    bool IsDeviceAvailable(string deviceName);
}