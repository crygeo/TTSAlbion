using LibAlbionProtocol.Models;
using LibAlbionProtocol.PacketModels;
using LibAlbionRouting.Dispatching;
using LibAlbionRouting.Routing;
using LibNetWork.Interfaces;

namespace LibAlbionRouting.Handlers;

/// <summary>
/// Routes Albion network events to the TTS pipeline.
///
/// Design decisions:
/// - User registration is now manual (set via <see cref="SetTrackedUser"/>);
///   automatic detection from CharacterStats was unreliable because loading
///   other players' stats also triggered it.
/// - <see cref="MessageSourceFilter"/> controls which chat event types are
///   forwarded. Changing the filter at runtime is safe (volatile read).
/// - The handler itself is stateless w.r.t. audio — it delegates to
///   <see cref="MessageService"/> which owns that pipeline.
/// </summary>
public sealed class GenericEventHandler : PacketHandler<EventPacket>, IEventDispatcher
{
    private readonly GameEventRouter _router = new();

    public void Subscribe<TModel>(EventCodes code, Func<TModel, Task> handler)
        where TModel : class
    {
        // El router mapea código → modelo → handler async
        _router.Subscribe<TModel>(code, model =>
        {
            _ = handler(model).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Console.WriteLine($"[EventDispatcher] Handler error for {code}: {t.Exception}");
            });
        });
    }

    protected override Task OnHandleAsync(EventPacket packet)
    {
        var code = (EventCodes)packet.EventCode;
        _router.TryRoute(code, packet.Parameters);
        return Task.CompletedTask;
    }

    
}