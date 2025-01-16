using RaftLibrary;

namespace WebSimulation;

public class SimulationNode : INode
{
    public readonly Node InnerNode;
    public SimulationNode(Node node)
    {
        this.InnerNode = node;
    }

    public int Id { get => InnerNode.Id; set => InnerNode.Id = value; }
    public int LeaderID { get => InnerNode.Id; set => InnerNode.Id = value; }
    public int Term { get => InnerNode.Term; set => InnerNode.Term = value; }
    public NodeState State { get => InnerNode.State; set => InnerNode.State = value; }
    public System.Timers.Timer Timer { get => InnerNode.Timer; set => InnerNode.Timer = value; }
    public List<int> Votes { get => InnerNode.Votes; set => InnerNode.Votes = value; }

    public Task ReceiveHeartbeat(int id)
    {
        ((INode)InnerNode).ReceiveHeartbeat(id);
        return Task.CompletedTask;
    }

    public Task RespondHeartbeat()
    {
        ((INode)InnerNode).RespondHeartbeat();  
        return Task.CompletedTask;
    }

    public Task SendVote()
    {
        return Task.CompletedTask;
    }

    public Task ReceiveRequestVote(int candidateId)
    {
        ((INode)InnerNode).ReceiveRequestVote(candidateId);
        return Task.CompletedTask;
    }
}
