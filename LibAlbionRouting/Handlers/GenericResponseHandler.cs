using LibAlbionProtocol.Models;
using LibAlbionRouting.Routing;
using LibNetWork.Interfaces;

namespace LibAlbionRouting.Handlers
{
    public class GenericResponseHandler : PacketHandler<ResponsePacket>
    {

        public GenericResponseRouter Router { get; } = new GenericResponseRouter();

        public GenericResponseHandler()
        {
        }

        protected override Task OnHandleAsync(ResponsePacket packet)
        {

            Router.TryRoute((OperationCodes)packet.OperationCode, packet.Parameters);

            return Task.CompletedTask;
        }
    }
}
