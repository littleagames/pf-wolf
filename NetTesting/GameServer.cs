using LiteNetLib;
using LiteNetLib.Utils;

namespace NetTesting;

internal sealed class GameServer
{
    private const string ConnectionKey = "game_key";

    private readonly int _port;
    private readonly int _maxConnections;
    private readonly EventBasedNetListener _listener;
    private readonly NetManager _server;

    public GameServer(int port, int maxConnections)
    {
        _port = port;
        _maxConnections = maxConnections;
        _listener = new EventBasedNetListener();
        _server = new NetManager(_listener);

        RegisterEvents();
    }

    private void RegisterEvents()
    {
        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;
    }

    public void Start()
    {
        _server.Start(_port);
        Console.WriteLine($"Server listening on port {_port}. Press ESC to stop.");

        while (true)//Console.ReadKey(intercept: true).Key != ConsoleKey.Escape)
        {
            _server.PollEvents();
            Thread.Sleep(15);
        }

        _server.Stop();
        Console.WriteLine("Server stopped.");
    }

    private void OnConnectionRequest(ConnectionRequest request)
    {
        if (_server.ConnectedPeersCount < _maxConnections)
        {
            request.AcceptIfKey(ConnectionKey);
            Console.WriteLine($"Connection request accepted from {request.RemoteEndPoint}");
        }
        else
        {
            request.Reject();
            Console.WriteLine($"Connection rejected (max {_maxConnections} peers reached).");
        }
    }

    private void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine($"[+] Client connected: {peer.Address} (Id: {peer.Id})");

        var writer = new NetDataWriter();
        writer.Put("Welcome to the server!");
        peer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        Console.WriteLine($"[-] Client disconnected: {peer.Address} — Reason: {info.Reason}");
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
    {
        var message = reader.GetString();
        Console.WriteLine($"[{peer.Address}] → \"{message}\"");

        // Echo the message back
        var writer = new NetDataWriter();
        writer.Put($"Echo: {message}");
        peer.Send(writer, DeliveryMethod.ReliableOrdered);

        reader.Recycle();
    }
}