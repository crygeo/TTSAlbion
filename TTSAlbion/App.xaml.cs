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
    
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        //D4__U-tozguvtHsQLNHEl09SXagU1WVK
        // Discord setup
        var discordClient = new DiscordSocketClient();
        
        await discordClient.LoginAsync(TokenType.Bot, "OTcxMDcwOTc5OTQ4MjkwMTA4.Gh9KhU.J4OtTAt2X0q0a7c7lNClcHIjzjo5vBEzPr0syA");
        await discordClient.StartAsync();

        // Composición del pipeline — todo por interfaz, fácil de mockear en tests
        var commandParser = new CommandParser("!!");
        var ttsEngine     = new WindowsTtsEngine();
        var wavConverter  = new WavToPcmConverter();
        var audioSink     = new DiscordAudioSink(discordClient, guildId: 1225900666308919316, voiceChannelId: 1424537377350746233);

        var messageService = new MessageService(commandParser, ttsEngine, wavConverter, audioSink);

        // Albion network stack
        var resolver   = new AlbionPortResolver(@"C:\...\Albion-Online.exe");
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
}