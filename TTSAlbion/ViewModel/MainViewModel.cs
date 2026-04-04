using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TTSAlbion.Interfaces;
using TTSAlbion.Services;
using TTSAlbion.ViewModel;

namespace TTSAlbion.ViewModels;

/// <summary>
/// ViewModel principal. Expone estado observable al View sin
/// referencias directas a Discord.Net ni a NetworkManager.
/// Depende únicamente de abstracciones inyectables y testeables.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IManualTtsCommand _manualTts;
    private readonly IDiscordInfoProvider _discordInfo;

    // --- Estado observable ---

    private string? _registeredUser;
    private bool _isSending;
    private string _manualText = string.Empty;
    private string? _feedbackMessage;
    private bool _isFeedbackError;

    public MainViewModel(IManualTtsCommand manualTts, IDiscordInfoProvider discordInfo)
    {
        _manualTts   = manualTts   ?? throw new ArgumentNullException(nameof(manualTts));
        _discordInfo = discordInfo ?? throw new ArgumentNullException(nameof(discordInfo));

        SpeakCommand = new AsyncRelayCommand(ExecuteSpeakAsync, CanSpeak);
    }

    // --- Propiedades expuestas al View ---

    /// <summary>Null = sin jugador detectado.</summary>
    public string? RegisteredUser
    {
        get => _registeredUser;
        private set
        {
            if (_registeredUser == value) return;
            _registeredUser = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasUser));
            OnPropertyChanged(nameof(UserInitials));
            OnPropertyChanged(nameof(UserStatusText));
        }
    }

    public bool HasUser => _registeredUser is not null;

    public string UserInitials => _registeredUser is { Length: > 0 }
        ? string.Concat(_registeredUser.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(w => char.ToUpperInvariant(w[0])))
        : "?";

    public string UserStatusText => HasUser ? "Activo" : "Sin detectar";

    public string GuildName   => _discordInfo.GuildName;
    public string ChannelName => _discordInfo.ChannelName;

    public string ManualText
    {
        get => _manualText;
        set
        {
            if (_manualText == value) return;
            _manualText = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)SpeakCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsSending
    {
        get => _isSending;
        private set
        {
            if (_isSending == value) return;
            _isSending = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)SpeakCommand).RaiseCanExecuteChanged();
        }
    }

    public string? FeedbackMessage
    {
        get => _feedbackMessage;
        private set { _feedbackMessage = value; OnPropertyChanged(); }
    }

    public bool IsFeedbackError
    {
        get => _isFeedbackError;
        private set { _isFeedbackError = value; OnPropertyChanged(); }
    }

    // --- Comando ---

    public ICommand SpeakCommand { get; }

    // --- API pública para que GenericEventHandler actualice el usuario ---

    public void SetRegisteredUser(string? username) => RegisteredUser = username;

    // --- Internos ---

    private bool CanSpeak() => !IsSending && ManualText.Trim().Length > 0;

    private async Task ExecuteSpeakAsync()
    {
        var text = ManualText?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        IsSending = true;
        FeedbackMessage = null;

        try
        {
            // Llamada asíncrona genérica al TTS + sink
            await _manualTts.SpeakAsync(text);

            IsFeedbackError = false;
            FeedbackMessage = "Enviado correctamente.";
            ManualText = string.Empty;
        }
        catch (OperationCanceledException)
        {
            // Cancelación limpia
            IsFeedbackError = true;
            FeedbackMessage = "Envío cancelado.";
        }
        catch (Exception ex)
        {
            IsFeedbackError = true;
            FeedbackMessage = "Error al enviar TTS";
            Console.WriteLine($"Error en ExecuteSpeakAsync: {ex}");
        }
        finally
        {
            IsSending = false;

            // Limpia feedback tras 3 segundos sin bloquear UI
            _ = ClearFeedbackAfterAsync(TimeSpan.FromSeconds(3));
        }
    }

    private async Task ClearFeedbackAfterAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay);
            FeedbackMessage = null;
        }
        catch
        {
            // ignorar errores de cancelación
        }
    }

    // --- INotifyPropertyChanged ---

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        if (_manualTts is IDisposable d) d.Dispose();
    }
}