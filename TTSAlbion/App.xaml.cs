using System.Configuration;
using System.Data;
using System.Windows;
using NetWorkLibrery.Interfazes;
using NetWorkLibrery.Modelos;
using NetWorkLibrery.Models;
using TTSAlbion.Albion;
using TTSAlbion.Albion.Handler.Event;
using TTSAlbion.Albion.Handler.Request;
using TTSAlbion.Albion.Handler.Response;

namespace TTSAlbion;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private NetworkManager _networkManager;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Ruta al ejecutable de Albion — idealmente desde configuración/settings
        const string albionPath = @"C:\Program Files (x86)\AlbionOnline\game\Albion-Online.exe";

        var resolver = new AlbionPortResolver(albionPath);
        var portFilter = new ResolvedPortFilter(resolver, cacheTtl: TimeSpan.FromSeconds(15));

        var parse = new AlbionParser();

        List<IPacketHandler> handlers = new()
        {
            new GenericEventHandler(),
            new GenericRequestHandler(),
            new GenericResponseHandler()
        };

        _networkManager = new NetworkManager(parse, handlers.ToArray(), portFilter);
        _networkManager.Start();
    }
}