using NetWorkLibrery.Interfazes;
using RequipAlbion.Network.Model;
using TTSAlbion.Albion.Handler.Event.Model;
using TTSAlbion.Services;

namespace TTSAlbion.Albion.Handler.Event;

public class GenericEventHandler : PacketHandler<EventPacket>
{
    // Router que maneja la distribución de eventos
    private readonly GameEventRouter Router = new();

    // Servicio
    private MessageService? _service;
    

    public GenericEventHandler()
    {

        _service = new MessageService();
        
        //Router.Subscribe<PartyPlayerJoinedModel>(EventCodes.PartyPlayerJoined, partyService.HandlePartyEvent);

        Router.Subscribe<MessageModel>(EventCodes.ChatMessage, model =>
        {
            _service.RunCommand(model);
        });
        
        Router.Subscribe<CharacterStatsModel>(EventCodes.CharacterStats, model =>
        {
            _service?.RegisterUser(model.NameUser);
        });
        
    }



    protected override Task OnHandleAsync(EventPacket packet)
    {
        Router.TryRoute((EventCodes)packet.EventCode, packet.Parameters);

        return Task.CompletedTask;
    }
}



