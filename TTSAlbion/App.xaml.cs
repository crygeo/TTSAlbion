using System.Configuration;
using System.Data;
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

namespace TTSAlbion;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private NetworkManager _networkManager;
    private Config _config;
    
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        //D4__U-tozguvtHsQLNHEl09SXagU1WVK
        
        //Configuración
        _config = GetConfig("Datos/config.json");
        
        
        // Discord setup
        var discordClient = new DiscordSocketClient();
        
        await discordClient.LoginAsync(TokenType.Bot, _config.Token);
        await discordClient.StartAsync();

        // Composición del pipeline — todo por interfaz, fácil de mockear en tests
        var commandParser = new CommandParser(_config.Prefix);
        var ttsEngine     = new WindowsTtsEngine();
        var wavConverter  = new WavToPcmConverter();
        var audioSink     = new DiscordAudioSink(discordClient, guildId: _config.GuildId, voiceChannelId: _config.VoiceChannelId);

        var messageService = new MessageService(commandParser, ttsEngine, wavConverter, audioSink);

        // Albion network stack
        var resolver   = new AlbionPortResolver(@_config.PathAlbion);
        var portFilter = new ResolvedPortFilter(resolver, cacheTtl: TimeSpan.FromSeconds(15));
        var parser     = new AlbionParser();

        _networkManager = new NetworkManager(
            parser,
            new IPacketHandler[]
            {
                new GenericEventHandler(messageService),  // ← inyectado
                new GenericRequestHandler(),
                new GenericResponseHandler()
            },
            portFilter);

        _networkManager.Start();
    }

    private Config GetConfig(string path)
    {
        var fileProvider = new PhysicalFileProvider();
        var pathResolver = new BaseDirectoryPathResolver();

        IJsonDeserializer jsonService = new NewtonsoftJsonDeserializer(fileProvider, pathResolver);

        var data = jsonService.FromFile<Config>(path);
        return data;
    }
    
}