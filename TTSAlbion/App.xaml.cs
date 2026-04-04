using System.Windows;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using NetWorkLibrery.Interfazes;
using NetWorkLibrery.Modelos;
using NetWorkLibrery.Models;
using TTSAlbion.Albion;
using TTSAlbion.Albion.Handler.Event;
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

namespace TTSAlbion;

public partial class App : Application
{
    private NetworkManager? _networkManager;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        NativeDependencyGuard.Verify();


        // --- Config ---
        var config = LoadConfig("Datos/config.json");

        // --- Discord ---
        var discordClient = await GetDiscordClient(config);

        // Espera real al evento Ready con timeout de seguridad
        

        // --- Servicios de audio ---
        ITtsEngine ttsEngine = new WindowsTtsEngine();
        IWavToPcmConverter wavConverter = new WavToPcmConverter(1);
        IAudioSink audioSink = new VirtualMicAudioSink();



        // --- Abstracciones para el ViewModel ---
        IManualTtsCommand manualTts = new ManualTtsCommand(ttsEngine, wavConverter, audioSink);
        IDiscordInfoProvider discordInfo = new DiscordInfoProvider(discordClient, config.GuildId, config.VoiceChannelId);

        // --- ViewModel ---
        var viewModel = new MainViewModel(manualTts, discordInfo);

        // --- MessageService: usa el mismo pipeline, actualiza el VM cuando detecta usuario ---
        var commandParser = new CommandParser(config.Prefix);
        var messageService = new MessageService(commandParser, ttsEngine, wavConverter, audioSink);

        // --- Red Albion ---
        var resolver = new AlbionPortResolver(config.PathAlbion);
        var portFilter = new ResolvedPortFilter(resolver, TimeSpan.FromSeconds(15));
        var parser = new AlbionParser();

        // GenericEventHandler notifica al ViewModel cuando detecta el usuario
        var eventHandler = new GenericEventHandler(messageService, username => viewModel.SetRegisteredUser(username));

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

        // --- Ventana principal ---
        var mainWindow = new MainWindow(viewModel);
        mainWindow.Show();
    }

    private static Config LoadConfig(string relativePath)
    {
        var fileProvider = new PhysicalFileProvider();
        var pathResolver = new BaseDirectoryPathResolver();
        IJsonDeserializer json = new NewtonsoftJsonDeserializer(fileProvider, pathResolver);
        return json.FromFile<Config>(relativePath);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _networkManager?.Stop();
        base.OnExit(e);
    }

    private async Task<DiscordSocketClient> GetDiscordClient(Config config)
    {
        var discordClient = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates,
            // Importante: evita que el gateway use el SynchronizationContext de WPF
            DefaultRetryMode = RetryMode.AlwaysRetry,
            EnableVoiceDaveEncryption = true
        });

        var readyTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        discordClient.Ready += () =>
        {
            readyTcs.TrySetResult();
            return Task.CompletedTask;
        };

        
        await discordClient.LoginAsync(TokenType.Bot, config.Token);
        await discordClient.StartAsync();
        
        await Task.WhenAny(readyTcs.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        
        return discordClient;
    }
}