using System.IO;
using System.Windows;
using NetWorkLibrery.Interfazes;
using NetWorkLibrery.Models;
using TTSAlbion.Albion;
using TTSAlbion.Albion.Handler.Request;
using TTSAlbion.Albion.Handler.Response;
using TTSAlbion.Converters;
using TTSAlbion.Datos;
using TTSAlbion.Infrastructure;
using TTSAlbion.Interfaces;
using TTSAlbion.Services;
using TTSAlbion.Services.Audio;
using TTSAlbion.Services.Tts;
using TTSAlbion.ViewModels;
using NetWorkLibrery.Modelos;

namespace TTSAlbion;

public partial class App : Application
{
    private NetworkManager? _networkManager;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logPath = Path.Combine(AppContext.BaseDirectory, "ttsalbion.log");
        FileConsoleLogger.Initialize(logPath);

        NativeDependencyGuard.Verify();

        // ── Settings repository ──────────────────────────────────────────────────
        // Single source of truth for persistence. Injected everywhere it is needed.
        var configPath = Path.Combine(AppContext.BaseDirectory, "Datos", "config.json");
        ISettingsRepository settingsRepo = new JsonSettingsRepository(configPath);

        // Load persisted config; fall back to safe defaults on first run.
        var config = settingsRepo.Load() ?? DefaultConfig();

        // ── Audio pipeline ───────────────────────────────────────────────────────
        ITtsEngine ttsEngine          = new WindowsTtsEngine();
        IAudioSinkFactory sinkFactory = new DefaultAudioSinkFactory();

        // Default sink on startup: VirtualMic (user can change at runtime).
        IAudioSink initialSink = await sinkFactory.Create(AudioSinkType.Local);

        // ── Command parser ───────────────────────────────────────────────────────
        ICommandParser commandParser = new CommandParser(string.IsNullOrWhiteSpace(config.Prefix) ? "!!" : config.Prefix);

        // ── Message service ──────────────────────────────────────────────────────
        var messageService = new MessageService(commandParser, ttsEngine, initialSink);

        // ── Network event handler ────────────────────────────────────────────────
        var eventHandler = new GenericEventHandler(messageService);
        eventHandler.SetTrackedUser(string.IsNullOrWhiteSpace(config.User) ? null : config.User);
        eventHandler.SetSourceFilter(MessageSourceFilter.ChatMessage | MessageSourceFilter.ChatSay);

        var deviceDetector = new NaudioDeviceDetector();
        var sinkAvailability = new SinkAvailabilityService(deviceDetector);
        
        var virtualMicStatus = sinkAvailability.GetAvailability()
            .First(x => x.Type == AudioSinkType.Local);

        string? initialStartupWarning = string.Empty;
        if (!virtualMicStatus.IsAvailable)
        {
            // Usamos el FeedbackMessage inicial del ViewModel, no un MessageBox
            // para no bloquear startup. El ViewModel lo expone en su estado inicial.
            initialStartupWarning = virtualMicStatus.UnavailableReason;
        }

        
        // ── ViewModel — receives both the factory deps and the loaded config ─────
        var viewModel = new MainViewModel(
            messageService,
            eventHandler,
            commandParser,
            sinkFactory,
            settingsRepo,
            config, 
            sinkAvailability,
            initialStartupWarning);           // <-- seeds UI fields from persisted values

        // ── Albion network ───────────────────────────────────────────────────────
        var resolver   = new AlbionPortResolver(config.PathAlbion);
        var portFilter = new ResolvedPortFilter(resolver, TimeSpan.FromSeconds(15));
        var parser     = new AlbionParser();

        _networkManager = new NetworkManager(
            parser,
            new IPacketHandler[]
            {
                eventHandler,
                new GenericRequestHandler(),
                new GenericResponseHandler()
            },
            portFilter);

        _networkManager.Start();

        // ── Window ───────────────────────────────────────────────────────────────
        new MainWindow(viewModel).Show();
        // new MainWindowV2(viewModel).Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _networkManager?.Stop();
        
        base.OnExit(e);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static Config DefaultConfig() => new()
    {
        Prefix          = "!!",
        User            = string.Empty,
        PathAlbion      = string.Empty,
        BotToken        = string.Empty,
        UserId      = 0,
    };
}
