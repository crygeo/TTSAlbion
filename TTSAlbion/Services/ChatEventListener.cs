// TTSAlbion/Services/ChatEventListener.cs

using LibAlbionProtocol.Models;
using LibAlbionProtocol.PacketModels;
using LibAlbionRouting.Dispatching;
using TTSAlbion.Interfaces;

namespace TTSAlbion.Services;

/// <summary>
/// Escucha eventos de chat de Albion y aplica lógica de negocio:
/// - Filtrado por usuario
/// - Filtrado por fuente (ChatMessage vs ChatSay)
/// - Parsing de comandos
/// - Encolamiento de síntesis
/// </summary>
public sealed class ChatEventListener : IDisposable
{
    private readonly ICommandParser _commandParser;
    private readonly Func<string, Task> _executeCommand;
    private volatile string? _trackedUser;
    private volatile MessageSourceFilter _sourceFilter = MessageSourceFilter.Both;

    public ChatEventListener(
        IEventDispatcher dispatcher,
        ICommandParser commandParser,
        Func<string, Task> executeCommand)
    {
        _commandParser = commandParser ?? throw new ArgumentNullException(nameof(commandParser));
        _executeCommand = executeCommand ?? throw new ArgumentNullException(nameof(executeCommand));

        // Suscribirse a eventos crudos — dispatcher no sabe qué hacemos
        dispatcher.Subscribe<ChatSayModel>(EventCodes.ChatSay, OnChatSayAsync);
        dispatcher.Subscribe<MessageModel>(EventCodes.ChatMessage, OnChatMessageAsync);
    }

    // ── Public API ──────────────────────────────────────────────────────────
    public void SetTrackedUser(string? username) => _trackedUser = username;
    public void SetSourceFilter(MessageSourceFilter filter) => _sourceFilter = filter;

    // ── Handlers privados (lógica de negocio) ───────────────────────────────
    private async Task OnChatSayAsync(ChatSayModel model)
    {
        if (!ShouldProcess(model.User, MessageSourceFilter.ChatSay))
            return;

        await ProcessMessageAsync(model.Message);
    }

    private async Task OnChatMessageAsync(MessageModel model)
    {
        if (!ShouldProcess(model.User, MessageSourceFilter.ChatMessage))
            return;

        await ProcessMessageAsync(model.Message);
    }

    private bool ShouldProcess(string playerName, MessageSourceFilter source)
    {
        if (_trackedUser is null)
            return false;

        if (!playerName.Equals(_trackedUser, StringComparison.OrdinalIgnoreCase))
            return false;

        if ((_sourceFilter & source) == 0)
            return false;

        return true;
    }

    private async Task ProcessMessageAsync(string message)
    {
        if (!_commandParser.TryParse(message, out var payload))
            return;

        await _executeCommand(payload);
    }

    public void Dispose() { }
}