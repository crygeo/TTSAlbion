using NetWorkLibrery.Interfazes;
using RequipAlbion.Network.Model;

namespace TTSAlbion.Albion.Handler.Event;

public class GenericEventHandler : PacketHandler<EventPacket>
{
    // Router que maneja la distribución de eventos
    private readonly GameEventRouter Router = new();

    // Servicio que maneja los eventos de Party

    public GenericEventHandler()
    {

        //Router.Subscribe<PartyPlayerJoinedModel>(EventCodes.PartyPlayerJoined, partyService.HandlePartyEvent);
        //Router.Subscribe<PartyPlayerJoinedModel>(EventCodes.PartyPlayerLeft, partyService.HandlePartyEvent);
        //Router.Subscribe<PartyPlayerJoinedModel>(EventCodes.PartyJoined, partyService.HandlePartyEvent);
        //Router.Subscribe<PartyPlayerJoinedModel>(EventCodes.PartyDisbanded, partyService.HandlePartyEvent);


    }



    protected override Task OnHandleAsync(EventPacket packet)
    {

        Router.TryRoute((EventCodes)packet.EventCode, packet.Parameters);

        return Task.CompletedTask;
    }
}



