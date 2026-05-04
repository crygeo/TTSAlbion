namespace LibNetWork.Interfaces;

public interface INetworkManager
{
    
    void Start();
    void Stop();
    bool IsAnySocketActive();
}