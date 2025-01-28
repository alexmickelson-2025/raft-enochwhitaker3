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
    public int AppendedEntry { get => InnerNode.AppendedEntry; set => InnerNode.AppendedEntry = value; }
    public int CommittedIndex { get => (int)InnerNode.CommittedIndex; set => InnerNode.CommittedIndex = value; }
    public int NextIndex { get => InnerNode.NextIndex; set => InnerNode.NextIndex = value; }
    public List<Entry> Log { get => InnerNode.Log; set => InnerNode.Log = value; }
    public Dictionary<int, string> StateMachine { get => InnerNode.StateMachine; set => InnerNode.StateMachine = value; }
    int? INode.CommittedIndex { get => InnerNode.CommittedIndex; set => InnerNode.CommittedIndex = value; }
    public bool IsRunning { get => InnerNode.IsRunning; set => InnerNode.IsRunning = value; }

    public async Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId, int? committedIndex, int prevLogIndex, int prevLogTerm, List<Entry>? newEntry = null)
    {
        await Task.Delay(NetworkDelay).ContinueWith(async (_previousTask) =>
        {
            await InnerNode.ReceiveHeartbeat(receivedTermId, receivedLeaderId, committedIndex, prevLogIndex, prevLogTerm);
        });
    }

    public async Task RespondHeartbeat(int id, int term, int logIndex, bool result, bool? addedToLog = null)
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

    public void EditLog(int removeAmount)
    {
        throw new NotImplementedException();
    }

    public List<Entry> GetLogList()
    {
        throw new NotImplementedException();
    }
}
