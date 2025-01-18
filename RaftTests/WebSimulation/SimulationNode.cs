using RaftLibrary;

namespace WebSimulation;

public class SimulationNode : INode
{
    public readonly Node InnerNode;
    public int NetworkDelay { get; set; }
    public SimulationNode(Node node, int? networkDelay = null)
    {
        this.InnerNode = node;
        NetworkDelay = networkDelay ?? 0;
    }

    public int Id { get => InnerNode.Id; set => InnerNode.Id = value; }
    public int LeaderID { get => InnerNode.LeaderId; set => InnerNode.LeaderId = value; }
    public int Term { get => InnerNode.Term; set => InnerNode.Term = value; }
    public NodeState State { get => InnerNode.State; set => InnerNode.State = value; }
    public System.Timers.Timer Timer { get => InnerNode.Timer; set => InnerNode.Timer = value; }
    public List<int> Votes { get => InnerNode.Votes; set => InnerNode.Votes = value; }
    public bool SimulationRunning { get; private set; } = false;


    public async Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId)
    {
        await Task.Delay(NetworkDelay).ContinueWith(async (_previousTask) =>
        {
            await InnerNode.ReceiveHeartbeat(receivedTermId, receivedLeaderId);
        });
    }

    public async Task RespondHeartbeat()
    {
        await Task.Delay(NetworkDelay).ContinueWith(async (_previousTask) =>
        {
            await InnerNode.RespondHeartbeat();
        });
    }

    public async Task SendVote()
    {
        await Task.Delay(NetworkDelay).ContinueWith(async (_previousTask) =>
        {
            await InnerNode.SendVote();
        });

    }

    public async Task ReceiveRequestVote(int candidateId)
    {
        await Task.Delay(NetworkDelay).ContinueWith(async (_previousTask) =>
        {
            await InnerNode.ReceiveRequestVote(candidateId);
        });
    }
}
