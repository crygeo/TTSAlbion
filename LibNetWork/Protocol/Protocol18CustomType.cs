namespace LibNetWork.Protocol;

internal sealed class Protocol18CustomType(byte typeCode, byte[] data)
{
    public byte TypeCode { get; } = typeCode;

    public byte[] Data { get; } = data;

    public override string ToString()
    {
        return $"Protocol18CustomType({TypeCode}, {Data.Length} bytes)";
    }
}