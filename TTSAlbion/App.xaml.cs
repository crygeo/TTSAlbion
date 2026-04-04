using System.Windows;
using Discord;
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

        // --- Config ---
        var config = LoadConfig("Datos/config.json");

        // --- Discord ---
        var discordClient = new DiscordSocketClient();
        await discordClient.LoginAsync(TokenType.Bot, config.Token);
        await discordClient.StartAsync();

        // Espera mínima para que el cliente resuelva guilds/channels
        await Task.Delay(2000);

        // --- Servicios de audio ---
        ITtsEngine    ttsEngine   = new WindowsTtsEngine();
        var wavConverter          = new WavToPcmConverter(2);
        
        //IAudioSink audioSink = new DiscordAudioSink(discordClient, config.GuildId, config.VoiceChannelId);
        IAudioSink audioSink = new LocalAudioSink (); // Para pruebas sin Discord, escribe PCM a disco

        // --- Abstracciones para el ViewModel ---
        IManualTtsCommand  manualTts   = new ManualTtsCommand(ttsEngine, wavConverter, audioSink);
        IDiscordInfoProvider discordInfo = new DiscordInfoProvider(discordClient, config.GuildId, config.VoiceChannelId);

        // --- ViewModel ---
        var viewModel = new MainViewModel(manualTts, discordInfo);

        // --- MessageService: usa el mismo pipeline, actualiza el VM cuando detecta usuario ---
        var commandParser  = new CommandParser(config.Prefix);
        var messageService = new MessageService(commandParser, ttsEngine, wavConverter, audioSink);

        // --- Red Albion ---
        var resolver   = new AlbionPortResolver(config.PathAlbion);
        var portFilter = new ResolvedPortFilter(resolver, TimeSpan.FromSeconds(15));
        var parser     = new AlbionParser();

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
}