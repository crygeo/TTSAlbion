using System.Collections;

namespace LibNetWork.Models;

internal sealed class SegmentedPackage(int totalLength)
{
    public int TotalLength { get; } = totalLength;

    public int ReceivedBytesCount { get; set; }

    public byte[] TotalPayload { get; } = new byte[totalLength];

    public BitArray ReceivedBytes { get; } = new(totalLength);
}