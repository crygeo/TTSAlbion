using NetWorkLibrery.Modelos;

namespace NetWorkLibrery.Interfazes;

public interface INetworkManager
{
    
    void Start();
    void Stop();
    bool IsAnySocketActive();
}