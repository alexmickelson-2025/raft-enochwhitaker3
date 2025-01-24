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
    public int AppendedEntry { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public int CommittedIndex { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public string CommandValue { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public int NextIndex { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public List<Entry> Log { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public Dictionary<int, string> StateMachine { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    int? INode.CommittedIndex { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public async Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId)
    {
        await Task.Delay(NetworkDelay).ContinueWith(async (_previousTask) =>
        {
            await InnerNode.ReceiveHeartbeat(receivedTermId, receivedLeaderId, 0, 0, 0);
        });
    }

    public async Task RespondHeartbeat()
    {
        await Task.Delay(NetworkDelay).ContinueWith(async (_previousTask) =>
        {
            await InnerNode.RespondHeartbeat(Id, Term, Log.Count - 1, true);
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

    public Task AppendEntriesRequest(string requestedEntry, int leaderId)
    {
        throw new NotImplementedException();
    }

    public Task AppendEntriesCommitted()
    {
        throw new NotImplementedException();
    }

    public Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId, int committedIndex, List<Entry>? newEntry = null)
    {
        throw new NotImplementedException();
    }

    public Task RespondHeartbeat(int term, int logIndex, bool? addedToLog = null)
    {
        throw new NotImplementedException();
    }

    public Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId, int committedIndex, int prevLogIndex, int prevLogTerm, List<Entry>? newEntry = null)
    {
        throw new NotImplementedException();
    }

    public Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId, int? committedIndex, int prevLogIndex, int prevLogTerm, List<Entry>? newEntry = null)
    {
        throw new NotImplementedException();
    }
}
