using NetWorkLibrery.Interfazes;
using RequipAlbion.Network.Handler.Request;
using RequipAlbion.Network.Model;

namespace TTSAlbion.Albion.Handler.Response
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
