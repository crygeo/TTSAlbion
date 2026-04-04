namespace NetWorkLibrery.Interfazes;

public interface IPacketHandler
{
    void SetNext(IPacketHandler handler);
    Task HandleAsync(object request);
}