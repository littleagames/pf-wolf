using NetTesting;

if (args.Length == 0 || args[0] == "server")
{
    Console.WriteLine("Starting server...");
    var server = new GameServer(port: 9050, maxConnections: 10);
    server.Start();
}
else if (args[0] == "client")
{
    Console.WriteLine("Starting client...");
    var client = new GameClient();
    client.Connect(host: "localhost", port: 9050);
}
else
{
    Console.WriteLine("Usage: NetTesting [server|client]");
}

// Terminal 1 - Server
// dotnet run -- server

// Terminal 2 - Client
// dotnet run -- client
