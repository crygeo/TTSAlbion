using NetWorkLibrery.Interfazes;
using RequipAlbion.Network.Model;
using TTSAlbion.Albion;
using TTSAlbion.Albion.Handler.Event;
using TTSAlbion.Albion.Handler.Event.Model;
using TTSAlbion.Services;

public sealed class GenericEventHandler : PacketHandler<EventPacket>
{
    private readonly GameEventRouter _router = new();

    // MessageService inyectado, no instanciado aquí
    public GenericEventHandler(MessageService messageService)
    {
        _router.Subscribe<MessageModel>(EventCodes.ChatMessage, model =>
            // Fire-and-forget con manejo de error — no bloquea el pipeline de red
            _ = messageService.RunCommandAsync(model)
                .ContinueWith(t => Console.WriteLine($"[TTS] Error: {t.Exception}"),
                    TaskContinuationOptions.OnlyOnFaulted));

        _router.Subscribe<CharacterStatsModel>(EventCodes.CharacterStats, model =>
            messageService.RegisterUser(model.NameUser));
    }

    protected override Task OnHandleAsync(EventPacket packet)
    {
        _router.TryRoute((EventCodes)packet.EventCode, packet.Parameters);
        return Task.CompletedTask;
    }
}