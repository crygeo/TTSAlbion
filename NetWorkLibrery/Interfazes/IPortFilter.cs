namespace NetWorkLibrery.Interfaces;

/// <summary>
/// Decides whether a packet's port pair is relevant for capture.
/// Implementations may be static (fixed ports) or dynamic (process-resolved).
/// </summary>
public interface IPortFilter
{
    bool Matches(int sourcePort, int destinationPort);
}