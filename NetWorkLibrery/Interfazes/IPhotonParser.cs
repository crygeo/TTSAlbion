namespace NetWorkLibrery.Interfazes;

public interface IPhotonParser
{
    void AddHandler(IPacketHandler handler);
    
    void ReceivePacket(byte[] payload);
    void OnRequest(byte operationCode, Dictionary<byte, object> parameters);
    void OnResponse(byte operationCode, short returnCode, string debugMessage, Dictionary<byte, object> parameters);
    void OnEvent(byte code, Dictionary<byte, object> parameters);

}