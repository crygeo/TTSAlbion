namespace NetWorkLibrery.Interfaces;

/// <summary>
/// Resolves the set of TCP/UDP ports currently held open by a specific process.
/// Decoupled from any game or application domain — knows only about OS networking.
/// </summary>
public interface IProcessPortResolver
{
    /// <summary>
    /// Returns the active ports for the target process.
    /// Returns an empty set if the process is not running or has no open ports.
    /// </summary>
    IReadOnlySet<int> Resolve();
}