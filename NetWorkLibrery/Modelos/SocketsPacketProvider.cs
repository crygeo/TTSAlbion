using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Channels;
using NetWorkLibrery.Interfaces;
using NetWorkLibrery.Interfazes;

namespace NetWorkLibrery.Modelos;

public class SocketsPacketProvider : PacketProvider
{
    private readonly IPhotonParser _photonReceiver;
    private readonly IPortFilter _portFilter;
    private readonly List<Socket> _sockets = new();
    private readonly List<IPAddress> _gateways = new();
    private byte[] _byteData = new byte[65000];
    private bool _stopReceiving;

    private readonly Channel<byte[]> _channel = Channel.CreateUnbounded<byte[]>();

    public SocketsPacketProvider(IPhotonParser photonReceiver, IPortFilter portFilter)
    {
        _photonReceiver = photonReceiver ?? throw new ArgumentNullException(nameof(photonReceiver));
        _portFilter = portFilter ?? throw new ArgumentNullException(nameof(portFilter));

        var hostEntries = GetAllHostEntries();
        SetGateway(hostEntries);
    }

    public override bool IsRunning => _sockets.Any(IsSocketActive);

    public override void Start()
    {
        _stopReceiving = false;
        foreach (var gateway in _gateways)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
            socket.Bind(new IPEndPoint(gateway, 0));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);

            byte[] byTrue = { 1, 0, 0, 0 };
            byte[] byOut = { 1, 0, 0, 0 };

            try
            {
                socket.IOControl(IOControlCode.ReceiveAll, byTrue, byOut);
            }
            catch (SocketException e)
            {
                Console.WriteLine(MethodBase.GetCurrentMethod());
                Console.WriteLine("{message}|{socketErrorCode}", MethodBase.GetCurrentMethod()?.DeclaringType,
                    e.SocketErrorCode);
                continue;
            }

            _ = ProcessLoop(socket); // Start processing loop
            _ = ReceiveDataAsync(socket); // Start receiving data asynchronously

            _sockets.Add(socket);
            Console.WriteLine(
                $"NetworkManager - Added Socket | AddressFamily: {socket.AddressFamily}, LocalEndPoint: {socket.LocalEndPoint}, " +
                $"Connected: {socket.Connected}, Available: {socket.Available}, Blocking: {socket.Blocking}, IsBound: {socket.IsBound}, " +
                $"ReceiveBufferSize: {socket.ReceiveBufferSize}, SendBufferSize: {socket.SendBufferSize}, Ttl: {socket.Ttl}");
        }
    }

    private async Task ProcessLoop(Socket socket)
    {
        await foreach (var packet in _channel.Reader.ReadAllAsync())
        {
            try
            {
                ProcessReceivedData(socket, packet);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Processing error: {ex}");
            }
        }
    }

    private void SetGateway(IEnumerable<IPHostEntry> hostEntries)
    {
        foreach (IPAddress ip in hostEntries.SelectMany(hostEntry => hostEntry.AddressList))
        {
            try
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    _gateways.Add(ip);
                }
            }
            catch
            {
                // ignored
            }
        }
    }

    private static bool IsSocketActive(Socket socket)
    {
        bool part1 = socket.Poll(1000, SelectMode.SelectRead);
        bool part2 = (socket.Available == 0);
        return !part1 || !part2;
    }

    private async Task ReceiveDataAsync(Socket socket)
    {
        while (!_stopReceiving)
        {
            try
            {
                int bytesReceived = await socket.ReceiveAsync(new ArraySegment<byte>(_byteData), SocketFlags.None);
                if (bytesReceived <= 0) continue;

                // Copiamos los bytes para que no se sobrescriban en el próximo ReceiveAsync
                var bufferCopy = new byte[bytesReceived];
                Array.Copy(_byteData, bufferCopy, bytesReceived);

                // Enviar a la cola para procesar
                await _channel.Writer.WriteAsync(bufferCopy);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Receive error: {ex}");
            }
        }
    }

    private static IEnumerable<IPHostEntry> GetAllHostEntries()
    {
        List<IPHostEntry> hostEntries = new List<IPHostEntry>();
        string hostName = Dns.GetHostName();
        IPHostEntry hostEntry = Dns.GetHostEntry(hostName);
        hostEntries.Add(hostEntry);
        return hostEntries;
    }

    public override void Stop()
    {
        
    }

    private void ProcessReceivedData(Socket socket, byte[] data)
    {
        using MemoryStream buffer = new MemoryStream(data, 0, data.Length);
        using BinaryReader read = new BinaryReader(buffer);
        read.BaseStream.Seek(2, SeekOrigin.Begin);
        ushort dataLength = (ushort)IPAddress.NetworkToHostOrder(read.ReadInt16());

        read.BaseStream.Seek(9, SeekOrigin.Begin);
        int protocol = read.ReadByte();

        if (protocol != 17)
        {
            return;
        }

        read.BaseStream.Seek(20, SeekOrigin.Begin);

        string srcPort = ((ushort)IPAddress.NetworkToHostOrder(read.ReadInt16())).ToString();
        string destPort = ((ushort)IPAddress.NetworkToHostOrder(read.ReadInt16())).ToString();

        if (!int.TryParse(srcPort, out int src) || !int.TryParse(destPort, out int dst))
            return;

        if (!_portFilter.Matches(src, dst))
            return;

        read.BaseStream.Seek(28, SeekOrigin.Begin);

        if (dataLength >= 28)
        {
            byte[] packetData = read.ReadBytes(dataLength - 28);
            _ = packetData.Reverse();

            try
            {
                _photonReceiver.ReceivePacket(packetData);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error ReceivePacket: " + ex);
            }
        }
    }
}