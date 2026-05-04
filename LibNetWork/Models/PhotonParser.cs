using System.Buffers;
using LibNetWork.Protocol;

namespace LibNetWork.Models;

public abstract class PhotonParser
{
    private const int CommandHeaderLength = 12;
    private const int PhotonHeaderLength = 12;

    private readonly Dictionary<int, SegmentedPackage> _pendingSegments = new();

    public void ReceivePacket(byte[] payload)
    {
        if (payload.Length < PhotonHeaderLength)
        {
            return;
        }

        int offset = 0;

        if (!Deserialize(out short _, payload, ref offset))
        {
            return;
        }

        if (!ReadByte(out byte flags, payload, ref offset))
        {
            return;
        }

        if (!ReadByte(out byte commandCount, payload, ref offset))
        {
            return;
        }

        if (!Deserialize(out int _, payload, ref offset))
        {
            return;
        }

        if (!Deserialize(out int _, payload, ref offset))
        {
            return;
        }

        bool isEncrypted = flags == 1;
        bool isCrcEnabled = flags == 0xCC;

        if (isEncrypted)
        {
            // Encrypted packages are not supported
            return;
        }

        if (isCrcEnabled)
        {
            int ignoredOffset = 0;
            if (!Deserialize(out int crc, payload, ref ignoredOffset))
            {
                return;
            }
            Serialize(0, payload, ref offset);

            if (crc != Calculate(payload, payload.Length))
            {
                // Invalid crc
                return;
            }
        }

        for (int commandIdx = 0; commandIdx < commandCount; commandIdx++)
        {
            HandleCommand(payload, ref offset);
        }
    }

    public void ReceivePacket(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return;
        }

        var tmp = new byte[payload.Length];
        payload.CopyTo(tmp);
        ReceivePacket(tmp);
    }

    public void ReceivePacket(ReadOnlySequence<byte> payload)
    {
        if (payload.Length == 0)
        {
            return;
        }

        if (payload.IsSingleSegment)
        {
            ReceivePacket(payload.FirstSpan);
            return;
        }

        var len = checked((int) payload.Length);
        var tmp = new byte[len];
        payload.CopyTo(tmp);
        ReceivePacket(tmp);
    }

    protected abstract void OnRequest(byte operationCode, Dictionary<byte, object> parameters);

    protected abstract void OnResponse(byte operationCode, short returnCode, string debugMessage, Dictionary<byte, object> parameters);

    protected abstract void OnEvent(byte code, Dictionary<byte, object> parameters);

    private void HandleCommand(byte[] source, ref int offset)
    {
        if (!ReadByte(out byte commandType, source, ref offset))
        {
            return;
        }
        if (!ReadByte(out byte _, source, ref offset))
        {
            return;
        }
        if (!ReadByte(out byte _, source, ref offset))
        {
            return;
        }
        // Skip 1 byte
        offset++;
        if (!Deserialize(out int commandLength, source, ref offset))
        {
            return;
        }
        if (!Deserialize(out int _, source, ref offset))
        {
            return;
        }
        commandLength -= CommandHeaderLength;

        switch ((CommandType) commandType)
        {
            case CommandType.Disconnect:
                {
                    return;
                }
            case CommandType.SendUnreliable:
                {
                    offset += 4;
                    commandLength -= 4;
                    goto case CommandType.SendReliable;
                }
            case CommandType.SendReliable:
                {
                    HandleSendReliable(source, ref offset, ref commandLength);
                    break;
                }
            case CommandType.SendFragment:
                {
                    HandleSendFragment(source, ref offset, ref commandLength);
                    break;
                }
            default:
                {
                    offset += commandLength;
                    break;
                }
        }
    }

    private void HandleSendReliable(byte[] source, ref int offset, ref int commandLength)
    {
        // Skip 1 byte
        offset++;
        commandLength--;
        ReadByte(out byte messageType, source, ref offset);
        commandLength--;

        int operationLength = commandLength;
        var payload = new Protocol18Stream(operationLength);
        payload.Write(source, offset, operationLength);
        payload.Seek(0L, SeekOrigin.Begin);

        offset += operationLength;
        switch ((MessageType) messageType)
        {
            case MessageType.OperationRequest:
                {
                    OperationRequest requestData = Protocol18Deserializer.DeserializeOperationRequest(payload);
                    OnRequest(requestData.OperationCode, requestData.Parameters);
                    break;
                }
            case MessageType.OperationResponse:
                {
                    OperationResponse responseData = Protocol18Deserializer.DeserializeOperationResponse(payload);
                    OnResponse(responseData.OperationCode, responseData.ReturnCode, responseData.DebugMessage, responseData.Parameters);
                    break;
                }
            case MessageType.Event:
                {
                    EventData eventData = Protocol18Deserializer.DeserializeEventData(payload);
                    OnEvent(eventData.Code, eventData.Parameters);
                    break;
                }
        }
    }

    private void HandleSendFragment(byte[] source, ref int offset, ref int commandLength)
    {
        if (!Deserialize(out int startSequenceNumber, source, ref offset))
        {
            return;
        }
        commandLength -= 4;
        if (!Deserialize(out int _, source, ref offset))
        {
            return;
        }
        commandLength -= 4;
        if (!Deserialize(out int _, source, ref offset))
        {
            return;
        }
        commandLength -= 4;
        if (!Deserialize(out int totalLength, source, ref offset))
        {
            return;
        }
        commandLength -= 4;
        if (!Deserialize(out int fragmentOffset, source, ref offset))
        {
            return;
        }
        commandLength -= 4;

        int fragmentLength = commandLength;
        if (totalLength <= 0 || fragmentLength <= 0)
        {
            return;
        }

        HandleSegmentedPayload(startSequenceNumber, totalLength, fragmentLength, fragmentOffset, source, ref offset);
    }

    private void HandleFinishedSegmentedPackage(byte[] totalPayload)
    {
        int offset = 0;
        int commandLength = totalPayload.Length;
        HandleSendReliable(totalPayload, ref offset, ref commandLength);
    }

    private void HandleSegmentedPayload(int startSequenceNumber, int totalLength, int fragmentLength, int fragmentOffset, byte[] source, ref int offset)
    {
        SegmentedPackage segmentedPackage = GetSegmentedPackage(startSequenceNumber, totalLength);

        if (fragmentOffset < 0 || fragmentLength <= 0 || fragmentOffset > segmentedPackage.TotalLength)
        {
            _pendingSegments.Remove(startSequenceNumber);
            return;
        }

        if (fragmentLength > segmentedPackage.TotalLength - fragmentOffset)
        {
            _pendingSegments.Remove(startSequenceNumber);
            return;
        }

        if (offset < 0 || offset > source.Length || fragmentLength > source.Length - offset)
        {
            _pendingSegments.Remove(startSequenceNumber);
            return;
        }

        Buffer.BlockCopy(source, offset, segmentedPackage.TotalPayload, fragmentOffset, fragmentLength);
        offset += fragmentLength;

        int fragmentEnd = fragmentOffset + fragmentLength;
        for (int index = fragmentOffset; index < fragmentEnd; index++)
        {
            if (segmentedPackage.ReceivedBytes[index])
            {
                continue;
            }

            segmentedPackage.ReceivedBytes[index] = true;
            segmentedPackage.ReceivedBytesCount++;
        }

        if (segmentedPackage.ReceivedBytesCount >= segmentedPackage.TotalLength)
        {
            _pendingSegments.Remove(startSequenceNumber);
            HandleFinishedSegmentedPackage(segmentedPackage.TotalPayload);
        }
    }

    private SegmentedPackage GetSegmentedPackage(int startSequenceNumber, int totalLength)
    {
        if (_pendingSegments.TryGetValue(startSequenceNumber, out SegmentedPackage? segmentedPackage))
        {
            if (segmentedPackage != null && segmentedPackage.TotalLength != totalLength)
            {
                _pendingSegments.Remove(startSequenceNumber);
                segmentedPackage = new SegmentedPackage(totalLength);
                _pendingSegments.Add(startSequenceNumber, segmentedPackage);
            }

            if (segmentedPackage != null)
            {
                return segmentedPackage;
            }
        }

        segmentedPackage = new SegmentedPackage(totalLength);
        _pendingSegments.Add(startSequenceNumber, segmentedPackage);

        return segmentedPackage;
    }

    private bool ReadByte(out byte value, byte[] source, ref int offset)
    {
        value = 0;

        if (offset < 0 || offset >= source.Length)
        {
            return false;
        }

        value = source[offset++];
        return true;
    }
    
    public static bool Deserialize(out int value, byte[] source, ref int offset)
    {
        value = 0;

        if (offset + 4 > source.Length)
        {
            return false;
        }

        var span = new Span<byte>(source, offset, 4);
        value = (span[0] << 24) | (span[1] << 16) | (span[2] << 8) | span[3];
        offset += 4;

        return true;
    }

    public static bool Deserialize(out short value, byte[] source, ref int offset)
    {
        value = 0;

        if (offset + 2 > source.Length)
        {
            return false;
        }

        var span = new Span<byte>(source, offset, 2);
        value = (short) ((span[0] << 8) | span[1]);
        offset += 2;

        return true;
    }
    
    public static void Serialize(int value, byte[] target, ref int offset)
    {
        target[offset] = (byte) (value >> 24);
        offset++;
        target[offset] = (byte) (value >> 16);
        offset++;
        target[offset] = (byte) (value >> 8);
        offset++;
        target[offset] = (byte) value;
        offset++;
    }
    
    public static uint Calculate(byte[] bytes, int length)
    {
        uint result = uint.MaxValue;
        uint key = 3988292384u;

        for (int i = 0; i < length; i++)
        {
            result ^= bytes[i];
            for (int j = 0; j < 8; j++)
            {
                if ((result & 1u) > 0u)
                {
                    result = result >> 1 ^ key;
                }
                else
                {
                    result >>= 1;
                }
            }
        }

        return result;
    }
}