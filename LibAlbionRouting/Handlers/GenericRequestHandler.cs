using LibAlbionProtocol.Models;
using LibAlbionRouting.Routing;
using LibNetWork.Interfaces;

namespace LibAlbionRouting.Handlers;

public class GenericRequestHandler : PacketHandler<RequestPacket>
{
    public GenericRequestRouter Router = new GenericRequestRouter();


    public GenericRequestHandler()
    {
    }

    protected override Task OnHandleAsync(RequestPacket packet)
    {
        Router.TryRoute((OperationCodes)packet.OperationCode, packet.Parameters);
        return Task.CompletedTask;
    }
}
