namespace RaftLibrary;
public interface INode
{
    public int Id { get; set; }
    public int LeaderID { get; set; }
    public int Term { get; set; }
    public NodeState State { get; set; }
    public System.Timers.Timer Timer {  get; set; }
    public List<int> Votes { get; set; }
    Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId);
    Task RespondHeartbeat();
    Task SendVote();
    Task ReceiveRequestVote(int candidateId);
}
