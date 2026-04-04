using System.Configuration;
using System.Data;
using System.Windows;
using NetWorkLibrery.Interfazes;
using NetWorkLibrery.Modelos;
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

        var parse = new AlbionParser();
        
        List<IPacketHandler> handlers = new List<IPacketHandler>
        {
            new GenericEventHandler(),
            new GenericRequestHandler(),
            new GenericResponseHandler()
        };
        _networkManager = new NetworkManager(parse, handlers.ToArray());
        
        _networkManager.Start();
    }
}