namespace LibNetWork.Interfaces;

public interface IReciverBuilder
{
    void AddHandler<TPacket>(PacketHandler<TPacket> handler);
    IPhotonParser Build();
}