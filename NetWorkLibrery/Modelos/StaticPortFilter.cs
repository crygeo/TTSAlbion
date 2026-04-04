using NetWorkLibrery.Interfaces;

namespace NetWorkLibrery.Models;

/// <summary>
/// Immutable filter based on a fixed set of ports.
/// Useful for testing, fallback, or known-static configurations.
/// </summary>
public sealed class StaticPortFilter : IPortFilter
{
    private readonly IReadOnlySet<int> _ports;

    public StaticPortFilter(IEnumerable<int> ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        _ports = new HashSet<int>(ports);
    }

    public bool Matches(int sourcePort, int destinationPort)
        => _ports.Contains(sourcePort) || _ports.Contains(destinationPort);
}