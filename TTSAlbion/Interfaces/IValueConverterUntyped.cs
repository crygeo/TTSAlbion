namespace TTSAlbion.Albion.Models.Converters;

/// <summary>
/// Non-generic bridge for <see cref="IValueConverter{T}"/> used in the hot path.
///
/// Why this exists:
/// The cache stores converters as <see cref="IValueConverterUntyped"/> so the
/// dispatch in <see cref="ModelHandler"/> requires zero generics, no DynamicInvoke,
/// and no boxing beyond the unavoidable object boundaries of the packet dictionary.
/// </summary>
public interface IValueConverterUntyped
{
    /// <summary>
    /// Converts <paramref name="rawValue"/> to the converter's output type, boxed as object.
    /// Returns <c>null</c> on failure; the caller decides whether to skip or throw.
    /// </summary>
    object? Convert(object rawValue);
}

/// <summary>
/// Wraps a typed <see cref="IValueConverter{T}"/> behind <see cref="IValueConverterUntyped"/>.
/// One instance is created per converter type at cache-build time and reused forever.
/// Implementations of <see cref="IValueConverter{T}"/> must therefore be stateless.
/// </summary>
internal sealed class ValueConverterAdapter<T> : IValueConverterUntyped
{
    private readonly IValueConverter<T> _inner;

    public ValueConverterAdapter(IValueConverter<T> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public object? Convert(object rawValue) => _inner.Convert(rawValue);
}