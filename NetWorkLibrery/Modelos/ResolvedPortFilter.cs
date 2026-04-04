using NetWorkLibrery.Interfaces;

namespace NetWorkLibrery.Models;

/// <summary>
/// Dynamic port filter that queries an IProcessPortResolver.
/// Caches results for a configurable TTL to avoid OS-level calls per packet
/// (GetExtendedTcpTable is not free — calling it at packet rate would be a bottleneck).
/// Thread-safe via Interlocked/volatile for the hot path.
/// </summary>
public sealed class ResolvedPortFilter : IPortFilter
{
    private readonly IProcessPortResolver _resolver;
    private readonly TimeSpan _cacheTtl;

    private volatile IReadOnlySet<int> _cachedPorts = new HashSet<int>();
    private long _lastResolvedTicks = 0;

    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(15);

    public ResolvedPortFilter(IProcessPortResolver resolver, TimeSpan? cacheTtl = null)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _cacheTtl = cacheTtl ?? DefaultTtl;
    }

    public bool Matches(int sourcePort, int destinationPort)
    {
        EnsureCacheValid();
        var ports = _cachedPorts;
        return ports.Contains(sourcePort) || ports.Contains(destinationPort);
    }

    private void EnsureCacheValid()
    {
        long now = Environment.TickCount64;
        long last = Interlocked.Read(ref _lastResolvedTicks);

        if (now - last < (long)_cacheTtl.TotalMilliseconds)
            return;

        // Only one thread refreshes the cache; others use stale data transiently — acceptable.
        if (Interlocked.CompareExchange(ref _lastResolvedTicks, now, last) != last)
            return;

        try
        {
            _cachedPorts = _resolver.Resolve();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ResolvedPortFilter] Failed to resolve ports: {ex.Message}");
            // Keep stale cache on failure — better than blocking capture entirely.
        }
    }

    /// <summary>Forces an immediate cache refresh on next Matches() call.</summary>
    public void Invalidate() => Interlocked.Exchange(ref _lastResolvedTicks, 0);
}