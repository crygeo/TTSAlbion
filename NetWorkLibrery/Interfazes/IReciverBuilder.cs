namespace NetWorkLibrery.Interfazes;

public interface IReciverBuilder
{
    void AddHandler<TPacket>(PacketHandler<TPacket> handler);
    IPhotonParser Build();
}