namespace TTSAlbion.Atributos;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ParseAttribute : Attribute
{
    public byte Key { get; }
    public Type? ConverterType { get; }

    public ParseAttribute(byte key)
    {
        Key = key;
    }

    public ParseAttribute(byte key, Type converterType)
    {
        Key = key;
        ConverterType = converterType;
    }
}
