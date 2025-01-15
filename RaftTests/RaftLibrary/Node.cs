using System.Threading;
using System.Timers;
using System.Xml.Linq;

namespace RaftLibrary;

public class Node
{
    public int Id { get; set; }
    public int Term { get; set; }
    public NodeState State { get; set; }
    public System.Timers.Timer Timer { get; set; }
    public Dictionary<int, int> Votes { get; set; }
    public List<INode> Nodes { get; set; }

    public Node(List<INode>? nodes = null)
    {
        Id = new Random().Next(1, 10000);
        State = NodeState.Follower;
        Votes = [];
        Nodes = nodes ?? [];
        Timer = new System.Timers.Timer(300);
        Timer.Elapsed += OnElectionTimeout;
        Timer.AutoReset = false;
        Timer.Start();
    }

    public async Task SendHeartbeat()
    {
        foreach(INode _node in Nodes)
        {
            {
            if (_node.State == NodeState.Follower)
                await _node.ReceiveHeartbeat(Term); 
            }
        }
    }

    public async Task ReceiveHeartbeat(int termId, int leaderId)
    {
        var leader = Nodes.Find(node => node.Id == leaderId);
        if (Timer.Enabled)
        {
            Timer.Stop();
        }

        if(termId! >= Term && leader != null) 
        {
            State = NodeState.Follower;
            ResetTimer();
            await leader.RespondHeartbeat();
        }
    }

    public async Task RespondHeartbeat()
    {
        await Task.CompletedTask;
    }

    public void ResetTimer()
    {
        Timer.Stop();
        int randomInterval = new Random().Next(150, 299);
        Timer.Interval = randomInterval;
        Timer.Start();
    }

    private void OnElectionTimeout(object sender, ElapsedEventArgs e)
    {
        ResetTimer();
        if (State != NodeState.Candidate)
        {
            BecomeCandidate();
        }
    }

    public void BecomeCandidate()
    {
        State = NodeState.Candidate;
        Term += 1;
        Votes.Add(Id, Term);
    }

    public void BecomeLeader()
    {
        Timer.Stop();
        State = NodeState.Leader;
        Timer = new System.Timers.Timer(50);
        Timer.Elapsed += async (sender, e) => await SendHeartbeat();
        Timer.AutoReset = true;
        Timer.Start();
    }
}
