using System.Timers;

namespace RaftLibrary;

public class Node
{
    public int Id { get; set; }
    public int LeaderId { get; set; }
    public int VotedTerm { get; set; }
    public int Term { get; set; }
    public NodeState State { get; set; }
    public System.Timers.Timer Timer { get; set; }
    public List<int> Votes { get; set; }
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

    public async Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId)
    {
        var leader = Nodes.Find(node => node.Id == receivedLeaderId);
        if (Timer.Enabled)
        {
            Timer.Stop();
        }

        if(receivedTermId! >= Term && leader != null) 
        {
            State = NodeState.Follower;
            LeaderId = receivedLeaderId;
            ResetTimer();
            await leader.RespondHeartbeat();
        }
    }

    public async Task RespondHeartbeat()
    {
        await Task.CompletedTask;
    }

    public async Task RequestVotes(int candidateId)
    {
        foreach (INode _node in Nodes.Where(node => node.State == NodeState.Follower))
        {
           await _node.ReceiveRequestVote(candidateId);
        }
    }

    public async Task ReceiveRequestVote(int candidateId)
    {
        var candidate = Nodes.Find(node => node.Id == candidateId);
        if (candidate != null && candidate.Term > Term && VotedTerm != candidate.Term)
            await candidate.SendVote();
            VotedTerm = Term + 1;

    }

    public async Task SendVote()
    {
        Votes.Add(Term);
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
        switch (State)
        {
            case NodeState.Follower:
                BecomeCandidate();
                break;
            case NodeState.Candidate:
                BecomeLeader();
                break;
        }

    }

    public void BecomeCandidate()
    {
        ResetTimer();
        State = NodeState.Candidate;
        Term += 1;
        Votes.Add(Term);
    }

    public async void BecomeLeader()
    {
        int votesReceived = Votes.Count(entry => entry == Term);
        int majorityNodes = (Nodes.Count + 1) / 2;

        if (votesReceived > majorityNodes && votesReceived > 1)
        {
            Timer.Stop();
            State = NodeState.Leader;
            LeaderId = Id;
            Timer = new System.Timers.Timer(50);
            Timer.Elapsed += async (sender, e) => await SendHeartbeat();
            Timer.AutoReset = true;
            await SendHeartbeat();
            Timer.Start();
        }
        else
        {
            ResetTimer();
        }
    }
}
