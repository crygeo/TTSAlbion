namespace NetWorkLibrery.Interfazes;

public interface IPhotonParser
{
    void AddHandler(IPacketHandler handler);

    // ReceivePacket, OnRequest, OnResponse, OnEvent son implementados
    // por PhotonParser (la clase base) — no se redeclaran aquí para
    // evitar ambigüedad. Si necesitas el contrato, usa PhotonParser directamente.
    void ReceivePacket(byte[] payload);
}