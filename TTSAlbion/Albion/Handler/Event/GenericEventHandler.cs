using NetWorkLibrery.Interfazes;
using RequipAlbion.Network.Model;
using TTSAlbion.Albion;
using TTSAlbion.Albion.Handler.Event;
using TTSAlbion.Albion.Handler.Event.Model;
using TTSAlbion.Services;

namespace TTSAlbion;

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
public sealed class GenericEventHandler : PacketHandler<EventPacket>
{
    private readonly GameEventRouter _router = new();
    private volatile string? _trackedUser;
    private volatile MessageSourceFilter _sourceFilter = MessageSourceFilter.Both;

    public GenericEventHandler(MessageService messageService)
    {
        // ChatMessage (zone / global chat)
        _router.Subscribe<MessageModel>(EventCodes.ChatMessage, model =>
        {
            if ((_sourceFilter & MessageSourceFilter.ChatMessage) == 0) return;
            DispatchIfTracked(model, messageService);
        });

        // ChatSay (/say in world)
        _router.Subscribe<ChatSayModel>(EventCodes.ChatSay, model =>
        {
            if ((_sourceFilter & MessageSourceFilter.ChatSay) == 0) return;
            DispatchIfTracked(model, messageService);
        });
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    /// <summary>Sets the player name whose messages trigger TTS. Null disables TTS.</summary>
    public void SetTrackedUser(string? username) => _trackedUser = username;

    /// <summary>Updates which chat sources are forwarded to the TTS pipeline.</summary>
    public void SetSourceFilter(MessageSourceFilter filter) => _sourceFilter = filter;

    // ── PacketHandler ────────────────────────────────────────────────────────────

    protected override Task OnHandleAsync(EventPacket packet)
    {
        _router.TryRoute((EventCodes)packet.EventCode, packet.Parameters);
        Console.WriteLine($"[GenericEventHandler] Routed event code {(EventCodes)packet.EventCode}");
        return Task.CompletedTask;
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    private void DispatchIfTracked(MessageModel model, MessageService messageService)
    {
        var tracked = _trackedUser;
        if (tracked is null) return;
        if (!model.User.Equals(tracked, StringComparison.OrdinalIgnoreCase)) return;

        _ = messageService.RunCommandAsync(model)
                          .ContinueWith(
                              t => Console.WriteLine($"[TTS] Error: {t.Exception}"),
                              TaskContinuationOptions.OnlyOnFaulted);
    }
    
    private void DispatchIfTracked(ChatSayModel model, MessageService messageService)
    {
        var tracked = _trackedUser;
        if (tracked is null) return;
        if (!model.User.Equals(tracked, StringComparison.OrdinalIgnoreCase)) return;

        _ = messageService.RunCommandAsync(model)
            .ContinueWith(
                t => Console.WriteLine($"[TTS] Error: {t.Exception}"),
                TaskContinuationOptions.OnlyOnFaulted);
    }

    
}