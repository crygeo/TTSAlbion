using NetWorkLibrery.Interfazes;
using RequipAlbion.Network.Model;
using TTSAlbion.Albion;
using TTSAlbion.Albion.Handler.Event;
using TTSAlbion.Albion.Handler.Event.Model;
using TTSAlbion.Services;

namespace TTSAlbion;

/// <summary>
/// Maneja eventos de red de Albion.
/// El callback onUserDetected desacopla este handler del ViewModel:
/// no hay referencia directa, solo una Action inyectada.
/// </summary>
public sealed class GenericEventHandler : PacketHandler<EventPacket>
{
    private readonly GameEventRouter _router = new();

    public GenericEventHandler(MessageService messageService, Action<string>? onUserDetected = null)
    {
        _router.Subscribe<MessageModel>(EventCodes.ChatMessage, model =>
            _ = messageService.RunCommandAsync(model)
                .ContinueWith(t => Console.WriteLine($"[TTS] Error: {t.Exception}"),
                    TaskContinuationOptions.OnlyOnFaulted));

        _router.Subscribe<CharacterStatsModel>(EventCodes.CharacterStats, model =>
        {
            messageService.RegisterUser(model.NameUser);
            onUserDetected?.Invoke(model.NameUser);
        });
    }

    protected override Task OnHandleAsync(EventPacket packet)
    {
        _router.TryRoute((EventCodes)packet.EventCode, packet.Parameters);
        return Task.CompletedTask;
    }
}