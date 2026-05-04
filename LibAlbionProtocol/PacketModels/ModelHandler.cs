namespace LibAlbionProtocol.PacketModels;

/// <summary>
/// Base class for packet model objects.
///
/// Mapping strategy (zero reflection on the hot path):
/// On the first instantiation of each concrete type, <see cref="PropertyAccessorCache"/>
/// builds a compiled <see cref="PropertyAccessor"/> array for that type and stores it
/// indefinitely. Every subsequent packet of the same type reuses the cached accessors.
///
/// Per-packet cost (after warm-up):
/// - One <see cref="PropertyAccessorCache.GetOrBuild"/> call → ConcurrentDictionary read (lock-free).
/// - One loop over the accessor array → compiled lambda invocations, no reflection.
/// - One <see cref="ResolvePrimitive"/> call per property → simple type-switch, no reflection.
///
/// Thread safety:
/// - The cache itself is thread-safe (ConcurrentDictionary).
/// - Model instances are never shared across threads; each packet produces its own instance.
/// </summary>
public abstract class ModelHandler
{
    protected ModelHandler(Dictionary<byte, object> parameters)
    {
        try
        {
            MapProperties(parameters);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[{GetType().Name}] Error mapping properties: {e}");
        }
    }

    // ── Hot path ─────────────────────────────────────────────────────────────────

    private void MapProperties(Dictionary<byte, object> parameters)
    {
        // ConcurrentDictionary read — lock-free after first build for this type.
        var accessors = PropertyAccessorCache.GetOrBuild(GetType());

        foreach (var accessor in accessors)
        {
            if (!parameters.TryGetValue(accessor.Key, out var rawValue) || rawValue is null)
                continue;

            var value = accessor.Converter is not null
                ? accessor.Converter.Convert(rawValue)
                : ResolvePrimitive(rawValue, accessor.TargetType);

            if (value is not null)
                accessor.Setter(this, value); // compiled lambda — no reflection
        }
    }

    // ── Primitive resolution ──────────────────────────────────────────────────────

    /// <summary>
    /// Converts <paramref name="rawValue"/> to <paramref name="targetType"/> using
    /// BCL Convert methods. TargetType is already Nullable-unwrapped by the accessor.
    ///
    /// This method is intentionally not virtual — subclasses should use
    /// <see cref="IValueConverter{T}"/> via <see cref="ParseAttribute"/> instead.
    /// </summary>
    private static object? ResolvePrimitive(object rawValue, Type targetType)
    {
        try
        {
            if (targetType == typeof(string))  return rawValue.ToString();
            if (targetType == typeof(int))     return Convert.ToInt32(rawValue);
            if (targetType == typeof(long))    return Convert.ToInt64(rawValue);
            if (targetType == typeof(short))   return Convert.ToInt16(rawValue);
            if (targetType == typeof(byte))    return Convert.ToByte(rawValue);
            if (targetType == typeof(float))   return Convert.ToSingle(rawValue);
            if (targetType == typeof(double))  return Convert.ToDouble(rawValue);
            if (targetType == typeof(bool))    return Convert.ToBoolean(rawValue);
            if (targetType == typeof(Guid))    return rawValue is byte[] b ? new Guid(b) : Guid.Parse(rawValue.ToString()!);
            if (targetType.IsEnum)             return Enum.ToObject(targetType, rawValue);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[ModelHandler] Failed to convert '{rawValue}' to {targetType.Name}: {ex.Message}");
        }

        return null;
    }
}