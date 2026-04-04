using System.Reflection;
using TTSAlbion.Albion.Models.Converters;
using TTSAlbion.Atributos;

namespace TTSAlbion.Albion.Models;

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
            Console.WriteLine($"[{GetType().Name}] Error al mapear propiedades: {e}");
        }
    }

    private void MapProperties(Dictionary<byte, object> parameters)
    {
        var props = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            var attr = prop.GetCustomAttribute<ParseAttribute>();
            if (attr is null) continue;
            if (!parameters.TryGetValue(attr.Key, out var rawValue) || rawValue is null) continue;

            var value = attr.ConverterType is not null
                ? ResolveWithConverter(attr.ConverterType, rawValue, prop.PropertyType)
                : ResolvePrimitive(rawValue, prop.PropertyType);

            if (value is not null)
                SetProperty(prop, value);
        }
    }

    // --- Resolución primitiva ---

    private static object? ResolvePrimitive(object rawValue, Type targetType)
    {
        // Unwrap Nullable<T> → T
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (type == typeof(string))  return rawValue.ToString();
            if (type == typeof(int))     return System.Convert.ToInt32(rawValue);
            if (type == typeof(long))    return System.Convert.ToInt64(rawValue);
            if (type == typeof(short))   return System.Convert.ToInt16(rawValue);
            if (type == typeof(byte))    return System.Convert.ToByte(rawValue);
            if (type == typeof(float))   return System.Convert.ToSingle(rawValue);
            if (type == typeof(double))  return System.Convert.ToDouble(rawValue);
            if (type == typeof(bool))    return System.Convert.ToBoolean(rawValue);
            if (type == typeof(Guid))    return rawValue is byte[] b ? new Guid(b) : Guid.Parse(rawValue.ToString()!);
            if (type.IsEnum)             return Enum.ToObject(type, rawValue);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModelHandler] Error convirtiendo '{rawValue}' a {type.Name}: {ex.Message}");
        }

        return null;
    }

    // --- Resolución via conversor ---

    private static object? ResolveWithConverter(Type converterType, object rawValue, Type targetType)
    {
        // Valida que el conversor implemente IValueConverter<T> y que T sea compatible con la propiedad
        var converterInterface = converterType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValueConverter<>));

        if (converterInterface is null)
        {
            Console.WriteLine($"[ModelHandler] {converterType.Name} no implementa IValueConverter<T>.");
            return null;
        }

        var outputType = converterInterface.GetGenericArguments()[0];
        if (!targetType.IsAssignableFrom(outputType))
        {
            Console.WriteLine($"[ModelHandler] {converterType.Name} produce {outputType.Name} pero la propiedad espera {targetType.Name}.");
            return null;
        }

        try
        {
            var converter = Activator.CreateInstance(converterType)
                ?? throw new InvalidOperationException($"No se pudo instanciar {converterType.Name}.");

            var method = converterType.GetMethod(nameof(IValueConverter<object>.Convert))
                ?? throw new InvalidOperationException($"{converterType.Name} no tiene método Convert.");

            return method.Invoke(converter, [rawValue]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModelHandler] Error ejecutando {converterType.Name}: {ex.Message}");
            return null;
        }
    }

    // --- Asignación via backing field (soporta init-only y get-only) ---

    private void SetProperty(PropertyInfo prop, object value)
    {
        var backingField = GetType().GetField(
            $"<{prop.Name}>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (backingField is not null)
        {
            backingField.SetValue(this, value);
            return;
        }

        // Fallback: setter público normal
        if (prop.CanWrite)
            prop.SetValue(this, value);
    }
}