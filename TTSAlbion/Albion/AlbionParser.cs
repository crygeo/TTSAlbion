using NetWorkLibrery.Interfazes;
using NetWorkLibrery.Modelos;
using RequipAlbion.Network.Model;

namespace TTSAlbion.Albion;

public class AlbionParser : IPhotonParser
{
    private readonly HandlersCollection _handlers;

    public AlbionParser()
    {
        _handlers = new HandlersCollection();
    }

    public void AddHandler(IPacketHandler handler)
    {
        _handlers.Add(handler);
    }

    public void OnEvent(byte code, Dictionary<byte, object> parameters)
    {
        short eventCode = ParseEventCode(parameters);

        if (eventCode <= -1)
        {
            return;
        }

        var eventPacket = new EventPacket(eventCode, parameters);

        _ = _handlers.HandleAsync(eventPacket);
    }

    public void ReceivePacket(byte[] payload)
    {
        throw new NotImplementedException();
    }

    public void OnRequest(byte operationCodeByte, Dictionary<byte, object> parameters)
    {
        short operationCode = ParseOperationCode(parameters);

        if (operationCode <= -1)
        {
            return;
        }

        var requestPacket = new RequestPacket(operationCode, parameters);

        _ = _handlers.HandleAsync(requestPacket);
    }

    public void OnResponse(byte operationCodeByte, short returnCode, string debugMessage, Dictionary<byte, object> parameters)
    {
        short operationCode = ParseOperationCode(parameters);

        if (operationCode <= -1)
        {
            return;
        }

        var responsePacket = new ResponsePacket(operationCode, parameters);

        _ = _handlers.HandleAsync(responsePacket);
    }

    private static short ParseEventCode(Dictionary<byte, object> parameters)
    {
        if (!parameters.TryGetValue(252, out object value))
        {
            // Other values are returned as -1 code.
            //throw new InvalidOperationException();
            return -1;
        }

        return (short)value;
    }

    private static short ParseOperationCode(Dictionary<byte, object> parameters)
    {
        if (!parameters.TryGetValue(253, out object value))
        {
            // Other values are returned as -1 code.
            //throw new InvalidOperationException();
            return -1;
        }

        return (short)value;
    }
}