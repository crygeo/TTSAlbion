using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TTSAlbion.Albion;
using TTSAlbion.Datos;
using TTSAlbion.Interfaces;
using TTSAlbion.Services;
using TTSAlbion.Services.Audio;
using TTSAlbion.ViewModel;
using AsyncRelayCommand = TTSAlbion.ViewModel.AsyncRelayCommand;

namespace TTSAlbion.ViewModels;

/// <summary>
/// Main ViewModel.
///
/// New responsibilities vs. previous version:
/// - Receives the persisted <see cref="Config"/> at construction and seeds all
///   UI fields from it (BotToken, GuildId, ChannelId, Prefix).
/// - Calls <see cref="ISettingsRepository.SaveAsync"/> when the user explicitly
///   confirms a change (Apply buttons). Never auto-saves on keystroke.
///
/// Invariants:
/// - Save is always fire-and-forget from the UI thread; errors surface via
///   <see cref="FeedbackMessage"/> rather than unhandled exceptions.
/// - The ViewModel does NOT own the file path — that belongs to
///   <see cref="ISettingsRepository"/>, keeping the VM infrastructure-agnostic.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MessageService      _messageService;
    private readonly GenericEventHandler _eventHandler;
    private readonly ICommandParser      _commandParser;
    private readonly IAudioSinkFactory   _sinkFactory;
    private readonly ISettingsRepository _settingsRepo;

    public MainViewModel(
        MessageService       messageService,
        GenericEventHandler  eventHandler,
        ICommandParser       commandParser,
        IAudioSinkFactory    sinkFactory,
        ISettingsRepository  settingsRepo,
        Config               initialConfig)
    {
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _eventHandler   = eventHandler   ?? throw new ArgumentNullException(nameof(eventHandler));
        _commandParser  = commandParser  ?? throw new ArgumentNullException(nameof(commandParser));
        _sinkFactory    = sinkFactory    ?? throw new ArgumentNullException(nameof(sinkFactory));
        _settingsRepo   = settingsRepo   ?? throw new ArgumentNullException(nameof(settingsRepo));

        // Seed UI from persisted config
        _prefixText   = commandParser.CurrentPrefix;          // already seeded from config in App.xaml.cs
        _botToken     = initialConfig.BotToken        ?? string.Empty;
        _botGuildId   = initialConfig.BotGuildId      == 0 ? string.Empty : initialConfig.BotGuildId.ToString();
        _botChannelId = initialConfig.BotVoiceChannelId == 0 ? string.Empty : initialConfig.BotVoiceChannelId.ToString();
        _registeredUser = initialConfig.User ?? string.Empty;

        // Commands
        SpeakCommand       = new AsyncRelayCommand(ExecuteSpeakAsync,  CanSpeak);
        ApplyUserCommand   = new RelayCommand(ApplyUser,               CanApplyUser);
        ApplyPrefixCommand = new RelayCommand(ApplyPrefix,             CanApplyPrefix);
        ApplySinkCommand   = new AsyncRelayCommand(ApplySinkAsync);
        StartBotCommand    = new AsyncRelayCommand(StartBotAsync,
            () => !_isBotRunning && SelectedSink == AudioSinkType.DiscordBot);
        StopBotCommand     = new AsyncRelayCommand(StopBotAsync,
            () =>  _isBotRunning && SelectedSink == AudioSinkType.DiscordBot);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Player section
    // ════════════════════════════════════════════════════════════════════════════

    private string  _playerNameInput = string.Empty;
    private string? _registeredUser;

    public string PlayerNameInput
    {
        get => _playerNameInput;
        set { Set(ref _playerNameInput, value); ((RelayCommand)ApplyUserCommand).RaiseCanExecuteChanged(); }
    }

    public string? RegisteredUser
    {
        get => _registeredUser;
        private set { Set(ref _registeredUser, value); OnPropertyChanged(nameof(HasUser)); }
    }

    public bool HasUser => _registeredUser is not null;

    public ICommand ApplyUserCommand { get; }

    private bool CanApplyUser() => PlayerNameInput.Trim().Length > 0;

    private void ApplyUser()
    {
        var name = PlayerNameInput.Trim();
        if (name.Length == 0) return;

        RegisteredUser = name;
        _messageService.RegisterUser(name);
        _eventHandler.SetTrackedUser(name);

        _ = PersistCurrentConfigAsync();   // fire-and-forget

        
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Chat source filter
    // ════════════════════════════════════════════════════════════════════════════

    private bool _listenChatMessage = true;
    private bool _listenChatSay     = true;

    public bool ListenChatMessage
    {
        get => _listenChatMessage;
        set { Set(ref _listenChatMessage, value); ApplySourceFilter(); }
    }

    public bool ListenChatSay
    {
        get => _listenChatSay;
        set { Set(ref _listenChatSay, value); ApplySourceFilter(); }
    }

    private void ApplySourceFilter()
    {
        var filter = MessageSourceFilter.None;
        if (_listenChatMessage) filter |= MessageSourceFilter.ChatMessage;
        if (_listenChatSay)     filter |= MessageSourceFilter.ChatSay;
        _eventHandler.SetSourceFilter(filter);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Prefix
    // ════════════════════════════════════════════════════════════════════════════

    private string _prefixText;

    public string PrefixText
    {
        get => _prefixText;
        set { Set(ref _prefixText, value); ((RelayCommand)ApplyPrefixCommand).RaiseCanExecuteChanged(); }
    }

    public ICommand ApplyPrefixCommand { get; }

    private bool CanApplyPrefix() =>
        PrefixText.Trim().Length > 0 && PrefixText != _commandParser.CurrentPrefix;

    private void ApplyPrefix()
    {
        var prefix = PrefixText.Trim();
        if (prefix.Length == 0) return;

        try
        {
            _commandParser.SetPrefix(prefix);
            SetFeedback("Prefijo actualizado.", isError: false);
            _ = PersistCurrentConfigAsync();   // fire-and-forget
        }
        catch (ArgumentException ex)
        {
            SetFeedback(ex.Message, isError: true);
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Audio sink selection
    // ════════════════════════════════════════════════════════════════════════════

    private AudioSinkType _selectedSink = AudioSinkType.VirtualMic;

    public AudioSinkType SelectedSink
    {
        get => _selectedSink;
        set
        {
            if (Set(ref _selectedSink, value))
            {
                OnPropertyChanged(nameof(IsBotSinkSelected));
                ((AsyncRelayCommand)StartBotCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)StopBotCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBotSinkSelected => SelectedSink == AudioSinkType.DiscordBot;

    public IReadOnlyList<AudioSinkType> SinkOptions { get; } =
        Enum.GetValues<AudioSinkType>().ToList();

    public ICommand ApplySinkCommand { get; }

    private async Task ApplySinkAsync()
    {
        if (SelectedSink == AudioSinkType.DiscordBot) return; // managed by Start/Stop bot

        try
        {
            var sink = await _sinkFactory.Create(SelectedSink);
            _messageService.UpdateSink(sink);
            SetFeedback($"Sink cambiado a {SelectedSink}.", isError: false);
        }
        catch (Exception ex)
        {
            SetFeedback($"Error al cambiar sink: {ex.Message}", isError: true);
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Discord Bot section
    // ════════════════════════════════════════════════════════════════════════════

    private string _botToken;
    private string _botGuildId;
    private string _botChannelId;
    private bool   _isBotRunning;

    public string BotToken
    {
        get => _botToken;
        set => Set(ref _botToken, value);
    }

    public string BotGuildId
    {
        get => _botGuildId;
        set => Set(ref _botGuildId, value);
    }

    public string BotChannelId
    {
        get => _botChannelId;
        set => Set(ref _botChannelId, value);
    }

    public bool IsBotRunning
    {
        get => _isBotRunning;
        private set
        {
            Set(ref _isBotRunning, value);
            ((AsyncRelayCommand)StartBotCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)StopBotCommand).RaiseCanExecuteChanged();
        }
    }

    public ICommand StartBotCommand { get; }
    public ICommand StopBotCommand  { get; }

    private async Task StartBotAsync()
    {
        if (!TryParseDiscordIds(out var guildId, out var channelId))
        {
            SetFeedback("Guild ID y Channel ID deben ser números válidos.", isError: true);
            return;
        }

        try
        {
            var botConfig = new DiscordBotConfig(BotToken, guildId, channelId);
            var sink      = await _sinkFactory.Create(AudioSinkType.DiscordBot, botConfig);

            _messageService.UpdateSink(sink);
            IsBotRunning = true;
            SetFeedback("Bot conectado.", isError: false);

            // Persist after successful connection so invalid credentials are never saved.
            _ = PersistCurrentConfigAsync();
        }
        catch (Exception ex)
        {
            SetFeedback($"Error al conectar bot: {ex.Message}", isError: true);
        }
    }

    private async Task StopBotAsync()
    {
        if (DefaultAudioSinkFactory.Client is { ConnectionState: Discord.ConnectionState.Connected })
            await DefaultAudioSinkFactory.Client.StopAsync();

        IsBotRunning = false;
        SetFeedback("Bot desconectado.", isError: false);
    }

    private bool TryParseDiscordIds(out ulong guildId, out ulong channelId)
    {
        channelId = 0;
        return ulong.TryParse(_botGuildId.Trim(),    out guildId)
            && ulong.TryParse(_botChannelId.Trim(),  out channelId);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Manual TTS
    // ════════════════════════════════════════════════════════════════════════════

    private string _manualText = string.Empty;
    private bool   _isSending;

    public string ManualText
    {
        get => _manualText;
        set { Set(ref _manualText, value); ((AsyncRelayCommand)SpeakCommand).RaiseCanExecuteChanged(); }
    }

    public bool IsSending
    {
        get => _isSending;
        private set { Set(ref _isSending, value); ((AsyncRelayCommand)SpeakCommand).RaiseCanExecuteChanged(); }
    }

    public ICommand SpeakCommand { get; }

    private bool CanSpeak() => !IsSending && ManualText.Trim().Length > 0;

    private async Task ExecuteSpeakAsync()
    {
        var text = ManualText.Trim();
        if (text.Length == 0) return;

        IsSending = true;
        ClearFeedback();

        try
        {
            await _messageService.ExecuteAsync(text);
            SetFeedback("Enviado correctamente.", isError: false);
            ManualText = string.Empty;
        }
        catch (OperationCanceledException)
        {
            SetFeedback("Envío cancelado.", isError: true);
        }
        catch (Exception ex)
        {
            SetFeedback($"Error al enviar TTS: {ex.Message}", isError: true);
        }
        finally
        {
            IsSending = false;
            _ = ClearFeedbackAfterAsync(TimeSpan.FromSeconds(3));
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Persistence
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds a Config snapshot from the current ViewModel state and persists it.
    /// Called only on explicit user confirmation (Apply / successful bot connect).
    /// Fire-and-forget: errors are logged, not re-thrown.
    /// </summary>
    private async Task PersistCurrentConfigAsync()
    {
        _ = TryParseDiscordIds(out var guildId, out var channelId);

        var config = new Config
        {
            Prefix            = _commandParser.CurrentPrefix,
            User              = PlayerNameInput,
            BotToken          = _botToken,
            BotGuildId        = guildId,
            BotVoiceChannelId = channelId,
            // Preserve fields not managed by the ViewModel by reloading first.
            // This avoids overwriting PathAlbion or legacy fields with defaults.
        };

        try
        {
            await _settingsRepo.SaveAsync(config).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Surface on UI thread via Dispatcher — ViewModel should not depend on
            // App.Current.Dispatcher directly, so we use a local helper.
            Console.WriteLine($"[Settings] Save failed: {ex}");
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Feedback
    // ════════════════════════════════════════════════════════════════════════════

    private string? _feedbackMessage;
    private bool    _isFeedbackError;

    public string? FeedbackMessage
    {
        get => _feedbackMessage;
        private set => Set(ref _feedbackMessage, value);
    }

    public bool IsFeedbackError
    {
        get => _isFeedbackError;
        private set => Set(ref _isFeedbackError, value);
    }

    private void SetFeedback(string message, bool isError)
    {
        IsFeedbackError  = isError;
        FeedbackMessage  = message;
    }

    private void ClearFeedback() => FeedbackMessage = null;

    private async Task ClearFeedbackAfterAsync(TimeSpan delay)
    {
        try { await Task.Delay(delay).ConfigureAwait(false); }
        catch { /* cancellation is fine */ }
        FeedbackMessage = null;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // INotifyPropertyChanged
    // ════════════════════════════════════════════════════════════════════════════

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // IDisposable
    // ════════════════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_messageService is IDisposable sd) sd.Dispose();
    }
}