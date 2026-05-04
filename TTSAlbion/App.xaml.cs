using System.IO;
using System.Net.Http;
using System.Windows;
using LibAlbionProtocol.Models;
using LibAlbionProtocol.Parsing;
using LibAlbionRouting.Dispatching;
using LibAlbionRouting.Handlers;
using LibNetWork.Interfaces;
using LibNetWork.Networking;
using TTSAlbion.Converters;
using TTSAlbion.Datos;
using TTSAlbion.Infrastructure;
using TTSAlbion.Interfaces;
using TTSAlbion.Services;
using TTSAlbion.Services.Audio;
using TTSAlbion.ViewModel;

namespace TTSAlbion;

public partial class App : Application
{
    private NetworkManager? _networkManager;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logPath = Path.Combine(AppContext.BaseDirectory, "ttsalbion.log");
        FileConsoleLogger.Initialize(logPath);


        // ── Settings repository ──────────────────────────────────────────────────
        // Single source of truth for persistence. Injected everywhere it is needed.
        var configPath = Path.Combine(AppContext.BaseDirectory, "Datos", "config.json");
        ISettingsRepository settingsRepo = new JsonSettingsRepository(configPath);

        // Load persisted config; fall back to safe defaults on first run.
        var config = settingsRepo.Load() ?? DefaultConfig();

        // ── Audio pipeline ───────────────────────────────────────────────────────
        ITtsEngine ttsEngine = new WindowsTtsEngine();
        IAudioSinkFactory sinkFactory = new DefaultAudioSinkFactory();

        // Default sink on startup: VirtualMic (user can change at runtime).
        IAudioSink initialSink = await sinkFactory.Create(AudioSinkType.Local);

        // ── Command parser ───────────────────────────────────────────────────────
        ICommandParser commandParser =
            new CommandParser(string.IsNullOrWhiteSpace(config.Prefix) ? "!!" : config.Prefix);

        
        // ── Translator ───────────────────────────────────────────────────────────────
        HttpClient client = new HttpClient();
        LangblyTranslatorService translator = new LangblyTranslatorService(client, config.TranslatorOptions);
        
        IEventDispatcher eventDispatcher = new GenericEventHandler();
        
        // ── Message service ──────────────────────────────────────────────────────
        var messageService = new MessageService(commandParser, ttsEngine, initialSink, translator)
        {
            SourceLang = config.SourceLang,
            TargetLang = config.TargetLang,
            UseTraslate = config.UseTraslate,
        };
        
        var chatListener = new ChatEventListener(eventDispatcher, commandParser, messageService.ExecuteAsync);
        chatListener.SetTrackedUser(config.UserInGame);
        chatListener.SetSourceFilter(config.MessageSourceFilter);
        
        // ── Logger opcional
        var eventLogger = new AlbionEventLogger(eventDispatcher);

        

        // ── Red ────────────────────────────────────────────────────────────────
        var resolver = new AlbionPortResolver(config.PathAlbion);
        var portFilter = new ResolvedPortFilter(resolver, TimeSpan.FromSeconds(15));
        var parser = new AlbionParser();
        
        var handlers = new IPacketHandler[]
        {
            eventDispatcher,               
            new GenericRequestHandler(),                           
            new GenericResponseHandler()                           
        };
        
        _networkManager = new NetworkManager(parser, handlers, portFilter);  

        
        // ── Sink Detector ────────────────────────────────────────────────

        
        var deviceDetector = new NaudioDeviceDetector();
        var sinkAvailability = new SinkAvailabilityService(deviceDetector);

        var virtualMicStatus = sinkAvailability.GetAvailability()
            .First(x => x.Type == config.AudioSinkType);

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
            chatListener,
            commandParser,
            sinkFactory,
            settingsRepo,
            config,
            sinkAvailability,
            initialStartupWarning); // <-- seeds UI fields from persisted values

        
        
       



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
        Prefix = "!!",
        UserInGame = string.Empty,
        UserIdDiscord = 0,

        MessageSourceFilter = MessageSourceFilter.ChatMessage | MessageSourceFilter.ChatSay,
        AudioSinkType = AudioSinkType.Local,

        UseTraslate = false,
        SourceLang = "en",
        TargetLang = "es",
        TranslatorOptions = new TranslatorOptions
        {
            ApiKey = string.Empty,
            BaseUrl = "https://api.langbly.com",
        },

        BotToken = string.Empty,

        PathAlbion = "C:\\...\\Albion-Online.exe",
    };
}
