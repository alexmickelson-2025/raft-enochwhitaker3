namespace RaftLibrary;
public interface INode
{
    public int Id { get; set; }
    public int LeaderID { get; set; }
    public int Term { get; set; }
    public NodeState State { get; set; }
    public System.Timers.Timer Timer {  get; set; }
    public Dictionary<int, int> Votes { get; set; }
    Task ReceiveHeartbeat(int id);
    Task RespondHeartbeat();
}
