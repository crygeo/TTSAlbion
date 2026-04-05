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

    protected async override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        NativeDependencyGuard.Verify();

        // ── Config ──────────────────────────────────────────────────────────────
        var config = LoadConfig("Datos/config.json");

        // ── Audio pipeline ───────────────────────────────────────────────────────
        ITtsEngine ttsEngine         = new WindowsTtsEngine();
        IAudioSinkFactory sinkFactory = new DefaultAudioSinkFactory();

        // Default sink: VirtualMic (user can change at runtime)
        IAudioSink initialSink = await sinkFactory.Create(AudioSinkType.VirtualMic);

        // ── Command parser ───────────────────────────────────────────────────────
        ICommandParser commandParser = new CommandParser(config.Prefix);

        // ── Message service ──────────────────────────────────────────────────────
        var messageService = new MessageService(commandParser, ttsEngine, initialSink);
        

        // ── Network event handler ────────────────────────────────────────────────
        var eventHandler = new GenericEventHandler(messageService);

        // ── ViewModel ────────────────────────────────────────────────────────────
        var viewModel = new MainViewModel( messageService, eventHandler, commandParser, sinkFactory);

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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _networkManager?.Stop();
        base.OnExit(e);
    }

    private static Config LoadConfig(string relativePath)
    {
        var fileProvider = new PhysicalFileProvider();
        var pathResolver = new BaseDirectoryPathResolver();
        IJsonDeserializer json = new NewtonsoftJsonDeserializer(fileProvider, pathResolver);
        return json.FromFile<Config>(relativePath);
    }
}