using LibAlbionProtocol.Interfaces;
using LibAlbionProtocol.Models;
using LibNetWork.Interfaces;
using LibNetWork.Models;

namespace LibAlbionProtocol.Parsing;

public class AlbionParser : PhotonParser, IAlbionPacketParser
{
    private readonly HandlersCollection _handlers;

    public AlbionParser()
    {
        _handlers = new HandlersCollection();
    }

    // --- Contrato IPhotonParser ---

    public void AddHandler(IPacketHandler handler)
    {
        _handlers.Add(handler);
    }

    // ReceivePacket ya está implementado por PhotonParser — NO la sobreescribas.
    // PhotonParser la parsea y llama a OnEvent/OnRequest/OnResponse internamente.

    // --- Callbacks de PhotonParser ---

    protected override void OnEvent(byte code, Dictionary<byte, object> parameters)
    {
        short eventCode = ParseCode(parameters, 252);
        if (eventCode < 0) return;

        _ = _handlers.HandleAsync(new EventPacket(eventCode, parameters));
    }

    protected override void OnRequest(byte operationCode, Dictionary<byte, object> parameters)
    {
        short opCode = ParseCode(parameters, 253);
        if (opCode < 0) return;

        _ = _handlers.HandleAsync(new RequestPacket(opCode, parameters));
    }

    protected override void OnResponse(byte operationCode, short returnCode, string debugMessage, Dictionary<byte, object> parameters)
    {
        short opCode = ParseCode(parameters, 253);
        if (opCode < 0) return;

        _ = _handlers.HandleAsync(new ResponsePacket(opCode, parameters));
    }

    // --- Helpers ---

    private static short ParseCode(Dictionary<byte, object> parameters, byte key)
    {
        if (!parameters.TryGetValue(key, out object? value))
            return -1;

        return value is short s ? s : (short)-1;
    }
}