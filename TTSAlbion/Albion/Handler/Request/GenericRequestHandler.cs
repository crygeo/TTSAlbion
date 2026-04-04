using NetWorkLibrery.Interfazes;
using RequipAlbion.Network.Handler.Request;
using RequipAlbion.Network.Model;

namespace TTSAlbion.Albion.Handler.Request;

public class GenericRequestHandler : PacketHandler<RequestPacket>
{
    public GenericRequestRouter Router => new GenericRequestRouter();


    public GenericRequestHandler()
    {
    }

    protected override Task OnHandleAsync(RequestPacket packet)
    {
        Router.TryRoute((OperationCodes)packet.OperationCode, packet.Parameters);
        return Task.CompletedTask;
    }
}
