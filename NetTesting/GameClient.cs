using LiteNetLib;
using LiteNetLib.Utils;

namespace NetTesting;

internal sealed class GameClient
{
    private const string ConnectionKey = "game_key";

    private readonly EventBasedNetListener _listener;
    private readonly NetManager _client;
    private NetPeer? _server;

    public GameClient()
    {
        _listener = new EventBasedNetListener();
        _client = new NetManager(_listener);

        RegisterEvents();
    }

    private void RegisterEvents()
    {
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;
    }

    public void Connect(string host, int port)
    {
        _client.Start();
        _client.Connect(host, port, ConnectionKey);

        Console.WriteLine($"Connecting to {host}:{port}... Type a message and press Enter. Press ESC to quit.");

        while (true)
        {
            _client.PollEvents();

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Escape)
                    break;

                if (key.Key == ConsoleKey.Enter && _server is not null)
                {
                    Console.Write("Message: ");
                    var message = Console.ReadLine();

                    if (!string.IsNullOrWhiteSpace(message))
                        Send(message);
                }
            }

            Thread.Sleep(15);
        }

        _client.Stop();
        Console.WriteLine("Client stopped.");
    }

    private void Send(string message)
    {
        var writer = new NetDataWriter();
        writer.Put(message);
        _server?.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private void OnPeerConnected(NetPeer peer)
    {
        _server = peer;
        Console.WriteLine($"[+] Connected to server: {peer.Address}");
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        _server = null;
         Console.WriteLine($"[-] Disconnected from server — Reason: {info.Reason}");
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
    {
        var message = reader.GetString();
        Console.WriteLine($"[Server] → \"{message}\"");
        reader.Recycle();
    }
}