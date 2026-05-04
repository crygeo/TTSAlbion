namespace LibNetWork.Interfaces;

public interface IPacketHandler
{
    void SetNext(IPacketHandler handler);
    Task HandleAsync(object request);
}