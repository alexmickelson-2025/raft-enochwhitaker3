using System.Text.Json;
using RaftLibrary;
using static RaftLibrary.DTOs;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8080");


var nodeId = Environment.GetEnvironmentVariable("NODE_ID") ?? throw new Exception("NODE_ID environment variable not set");
var otherNodesRaw = Environment.GetEnvironmentVariable("OTHER_NODES") ?? throw new Exception("OTHER_NODES environment variable not set");
var nodeIntervalScalarRaw = Environment.GetEnvironmentVariable("NODE_INTERVAL_SCALAR") ?? throw new Exception("NODE_INTERVAL_SCALAR environment variable not set");

builder.Services.AddLogging();
var serviceName = "Node" + nodeId;

var app = builder.Build();

var logger = app.Services.GetService<ILogger<Program>>();

Console.WriteLine($"Node ID {nodeId}");
Console.WriteLine($"Other nodes environment config: {otherNodesRaw}");

List<INode> otherNodes = otherNodesRaw
  .Split(";")
  .Select(s => (INode)new HttpRpcOtherNode(int.Parse(s.Split(",")[0]), s.Split(",")[1]))
  .ToList();

Console.WriteLine($"other nodes {JsonSerializer.Serialize(otherNodes)}");


var node = new Node(otherNodes)
{
    Id = int.Parse(nodeId)
};

node.MinInterval = 1500;
node.MaxInterval = 3000;


app.MapGet("/health", () => "healthy");

app.MapGet("/nodeData", () =>
{
    return new NodeData
    {
        Id = node.Id,
        Timer = node.Timer,
        IsRunning = node.IsRunning,
        Term = node.Term,
        LeaderId = node.LeaderId,
        CommittedIndex = node.CommittedIndex,
        Log = node.Log,
        State = node.State,
        StateMachine = node.StateMachine,
        MinInterval = node.MinInterval,
        MaxInterval = node.MaxInterval,
        StartTime = node.StartTime,
        ElapsedTime = node.ElapsedTime,
    };

});


app.MapPost("/request/votes", async (RequestVoteDTO Data) =>
{
    Console.WriteLine($"received request votes {Data}");
    await node.RequestVotes(Data);
});

app.MapPost("/receive/heartbeat", async (ReceiveHeartbeatDTO Data) =>
{
    Console.WriteLine($"received heartbeat {Data}");
    await node.ReceiveHeartbeat(Data);
});

app.MapPost("/respond/heartbeat", async (RespondHeartbeatDTO Data) =>
{
    Console.WriteLine($"received append entries response {Data}");
    await node.RespondHeartbeat(Data);
});

app.MapPost("/receive/requestvote", async (ReceiveRequestVoteDTO Data) =>
{

    Console.WriteLine($"received vote response {Data}");
    await node.ReceiveRequestVote(Data);
});

app.MapPost("/receive/command", async (ClientCommandData Data) =>
{

    Console.WriteLine($"received vote response {Data}");
    await node.ReceiveClientCommand(Data);
});

app.MapPost("/receive/vote", async () =>
{
   await node.SendVote();
});

app.MapPost("/receive/togglePause", async (TogglePauseData data) =>
{
    await node.TogglePause(data.toggle);
});

app.Run();