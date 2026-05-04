using System.Linq.Expressions;
using System.Reflection;
using LibAlbionProtocol.Interfaces;
using LibAlbionProtocol.Parsing;

namespace LibAlbionProtocol.PacketModels;

/// <summary>
/// Compiled metadata for a single mapped property.
/// Built once per (Type, Property) pair and cached indefinitely.
///
/// Design:
/// - <see cref="Setter"/> is a compiled lambda — zero reflection cost on the hot path.
/// - <see cref="Converter"/> is a singleton instance per converter type, reused across packets.
///   Converter implementations must be stateless (they receive rawValue and return a result).
/// - <see cref="TargetType"/> is the unwrapped Nullable&lt;T&gt; target, resolved at build time.
/// </summary>
internal sealed class PropertyAccessor
{
    /// <summary>Packet parameter key (from <see cref="ParseAttribute.Key"/>).</summary>
    public byte Key { get; }

    /// <summary>
    /// The CLR type to convert into, with Nullable unwrapped.
    /// e.g. <c>int?</c> → <c>int</c>.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Compiled setter: <c>(instance, boxedValue) => instance.Property = (T)value</c>.
    /// Equivalent in speed to a direct property assignment.
    /// </summary>
    public Action<object, object> Setter { get; }

    /// <summary>
    /// Optional reusable converter instance. Null when primitive conversion applies.
    /// The instance is shared across all packets of this model type — must be stateless.
    /// </summary>
    public IValueConverterUntyped? Converter { get; }

    private PropertyAccessor(
        byte key,
        Type targetType,
        Action<object, object> setter,
        IValueConverterUntyped? converter)
    {
        Key        = key;
        TargetType = targetType;
        Setter     = setter;
        Converter  = converter;
    }

    // ── Factory ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to build a <see cref="PropertyAccessor"/> for <paramref name="prop"/>.
    /// Returns <c>null</c> if the property lacks a <see cref="ParseAttribute"/>
    /// or if the converter type is invalid (logged to console).
    /// </summary>
    public static PropertyAccessor? TryBuild(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<ParseAttribute>();
        if (attr is null) return null;

        var rawTarget    = prop.PropertyType;
        var targetType   = Nullable.GetUnderlyingType(rawTarget) ?? rawTarget;
        var setter       = BuildSetter(prop);
        var converter    = attr.ConverterType is not null
                               ? ResolveConverter(attr.ConverterType, targetType, prop.Name)
                               : null;

        // If a converter type was declared but is invalid, skip this property entirely
        // to avoid silently assigning wrong data.
        if (attr.ConverterType is not null && converter is null)
            return null;

        return new PropertyAccessor(attr.Key, targetType, setter, converter);
    }

    // ── Setter compilation ───────────────────────────────────────────────────────

    /// <summary>
    /// Compiles: <c>(object instance, object value) => ((TModel)instance).Prop = (TProp)value</c>
    ///
    /// Handles:
    /// - Normal read/write properties.
    /// - Init-only properties and get-only auto-properties via the backing field.
    ///   The backing field is resolved at build time, not per-packet.
    /// </summary>
    private static Action<object, object> BuildSetter(PropertyInfo prop)
    {
        // Prefer backing field so init-only props work without extra IL tricks.
        var backingField = prop.DeclaringType!.GetField(
            $"<{prop.Name}>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (backingField is not null)
            return BuildFieldSetter(backingField);

        if (!prop.CanWrite)
            throw new InvalidOperationException(
                $"Property '{prop.DeclaringType.Name}.{prop.Name}' has no setter and no backing field.");

        return BuildPropertySetter(prop);
    }

    private static Action<object, object> BuildFieldSetter(FieldInfo field)
    {
        // Parameters
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var valueParam    = Expression.Parameter(typeof(object), "value");

        // Cast instance to the declaring type, cast value to the field type
        var castInstance  = Expression.Convert(instanceParam, field.DeclaringType!);
        var castValue     = Expression.Convert(valueParam, field.FieldType);

        // instance.field = value
        var assign = Expression.Assign(Expression.Field(castInstance, field), castValue);

        return Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam).Compile();
    }

    private static Action<object, object> BuildPropertySetter(PropertyInfo prop)
    {
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var valueParam    = Expression.Parameter(typeof(object), "value");

        var castInstance  = Expression.Convert(instanceParam, prop.DeclaringType!);
        var castValue     = Expression.Convert(valueParam, prop.PropertyType);

        var assign = Expression.Assign(Expression.Property(castInstance, prop), castValue);

        return Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam).Compile();
    }

    // ── Converter resolution ─────────────────────────────────────────────────────

    private static IValueConverterUntyped? ResolveConverter(
        Type converterType, Type targetType, string propName)
    {
        // Validate IValueConverter<T> implementation
        var converterInterface = converterType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IValueConverter<>));

        if (converterInterface is null)
        {
            Console.WriteLine(
                $"[PropertyAccessor] {converterType.Name} does not implement IValueConverter<T>. " +
                $"Property '{propName}' will be skipped.");
            return null;
        }

        var outputType = converterInterface.GetGenericArguments()[0];
        if (!targetType.IsAssignableFrom(outputType))
        {
            Console.WriteLine(
                $"[PropertyAccessor] {converterType.Name} produces {outputType.Name} " +
                $"but property '{propName}' expects {targetType.Name}. Property will be skipped.");
            return null;
        }

        try
        {
            var instance = Activator.CreateInstance(converterType)
                ?? throw new InvalidOperationException($"Activator returned null for {converterType.Name}.");

            // Wrap in the untyped adapter so the hot path needs no generics or DynamicInvoke
            var adapterType = typeof(ValueConverterAdapter<>).MakeGenericType(outputType);
            return (IValueConverterUntyped)Activator.CreateInstance(adapterType, instance)!;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[PropertyAccessor] Failed to instantiate converter {converterType.Name}: {ex.Message}. " +
                $"Property '{propName}' will be skipped.");
            return null;
        }
    }
}