using NetWorkLibrery.Interfazes;

namespace NetWorkLibrery.Modelos;

public class NetworkManager : INetworkManager
{
    private readonly PacketProvider _packetProvider;
    

    public NetworkManager(IPhotonParser parser, IPacketHandler[] handler)
    {
        _packetProvider = new SocketsPacketProvider(Build(parser, handler));
    }

    private static IPhotonParser Build(IPhotonParser parser, IPacketHandler[]  handlers)
    {
        ReceiverBuilder builder = new ReceiverBuilder(parser);
        foreach (var handler in handlers)
            builder.AddHandler(handler);
            
        return builder.Build();
    }

    public void Start()
    {
        Console.WriteLine("Start Capture");

        _packetProvider.Start();

    }

    public void Stop()
    {
        Console.WriteLine("Stop Capture");

        _packetProvider.Stop();

    }

    public bool IsAnySocketActive()
    {
        return _packetProvider.IsRunning;
    }
}