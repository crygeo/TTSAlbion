using System.Collections.Concurrent;
using System.Reflection;

namespace TTSAlbion.Albion;

/// <summary>
/// Builds and caches <see cref="PropertyAccessor"/> arrays per model type.
///
/// Design decisions:
/// - <see cref="ConcurrentDictionary{TKey,TValue}"/> with <c>GetOrAdd</c> is the
///   canonical pattern for lazy, thread-safe, one-time initialization per key.
/// - <c>GetOrAdd</c> may invoke the factory more than once under contention,
///   but the result is always correct (idempotent build) and the duplicate
///   instance is simply discarded — acceptable for a startup-time operation.
/// - The cache is application-scoped (static) because model types are fixed
///   at compile time and never change at runtime.
/// - Only public instance properties decorated with [Parse] are included;
///   everything else is ignored at build time, not per-packet.
/// </summary>
internal static class PropertyAccessorCache
{
    private static readonly ConcurrentDictionary<Type, PropertyAccessor[]> Cache = new();

    /// <summary>
    /// Returns the cached accessors for <paramref name="modelType"/>,
    /// building them on the first call for that type.
    /// </summary>
    public static PropertyAccessor[] GetOrBuild(Type modelType)
    {
        return Cache.GetOrAdd(modelType, BuildAccessors);
    }

    private static PropertyAccessor[] BuildAccessors(Type type)
    {
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var accessors = new List<PropertyAccessor>(props.Length);

        foreach (var prop in props)
        {
            try
            {
                var accessor = PropertyAccessor.TryBuild(prop);
                if (accessor is not null)
                    accessors.Add(accessor);
            }
            catch (Exception ex)
            {
                // Log and continue — a bad property must not crash the entire model.
                Console.WriteLine(
                    $"[PropertyAccessorCache] Failed to build accessor for " +
                    $"'{type.Name}.{prop.Name}': {ex.Message}");
            }
        }

        return accessors.ToArray();
    }
}