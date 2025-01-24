namespace RaftLibrary;
public interface INode
{
    public int Id { get; set; }
    public int LeaderID { get; set; }
    public int Term { get; set; }
    public int AppendedEntry { get; set; }
    public int? CommittedIndex { get; set; }
    public int NextIndex { get; set; }
    public string CommandValue { get; set; }
    public NodeState State { get; set; }
    public System.Timers.Timer Timer {  get; set; }
    public List<int> Votes { get; set; }
    public List<Entry> Log { get; set; }
    public Dictionary<int, string> StateMachine { get; set; }
    Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId, int? committedIndex, int prevLogIndex, int prevLogTerm, List<Entry>? newEntry = null);
    Task RespondHeartbeat(int term, int logIndex, bool? addedToLog = null);
    Task SendVote();
    Task ReceiveRequestVote(int candidateId);
    Task AppendEntriesRequest(string requestedEntry, int leaderId);
    Task AppendEntriesCommitted();
}
