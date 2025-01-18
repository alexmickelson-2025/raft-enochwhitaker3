using System.Timers;

namespace RaftLibrary;

public class Node
{
    public int Id { get; set; }
    public int LeaderId { get; set; }
    public int VotedTerm { get; set; }
    public int Term { get; set; }
    public NodeState State { get; set; }
    public int MinInterval { get; set; }
    public int MaxInterval { get; set; }
    public int LeaderInterval { get; set; }
    public System.Timers.Timer Timer { get; set; }
    public DateTime StartTime { get; set; }
    public  double ElapsedTime { get; set; }
    public List<int> Votes { get; set; }
    public List<INode> Nodes { get; set; }

    public Node(List<INode>? nodes = null, int? minInterval = null, int? maxInterval = null, int? leaderInterval = null)
    {
        Id = new Random().Next(1, 10000);
        State = NodeState.Follower;
        Votes = [];
        Nodes = nodes ?? [];
        MinInterval = minInterval ?? 150;
        MaxInterval = maxInterval ?? 300;
        LeaderInterval = leaderInterval ?? 50;

        int randomInterval = new Random().Next(MinInterval, MaxInterval);
        Timer = new System.Timers.Timer(randomInterval);
        Timer.Elapsed += OnElectionTimeout;
        Timer.AutoReset = false;
        Timer.Start();
        StartTime = DateTime.Now;
    }

    public async Task SendHeartbeat()
    {
        StartTime = DateTime.Now;
        foreach (INode _node in Nodes)
        {
            await _node.ReceiveHeartbeat(Term, Id);
        }
        StartLeaderTimer();
    }

    public void StartLeaderTimer()
    {
        Timer.Stop();
        Timer.Dispose();
        Timer = new System.Timers.Timer(LeaderInterval);
        Timer.Elapsed += async (sender, e) => await SendHeartbeat();
        Timer.AutoReset = false;
        Timer.Start();
    }

    public async Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId)
    {
        var leader = Nodes.Find(node => node.Id == receivedLeaderId);

        if (receivedTermId >= Term && leader != null)
        {
            if(State == NodeState.Leader && receivedTermId == Term)
                await Task.CompletedTask;
            
            else
            {
                State = NodeState.Follower;
                LeaderId = receivedLeaderId;
                Term = receivedTermId;
                Votes.Clear();
                ResetTimer();
                await leader.RespondHeartbeat();
            }
        }
    }

    public async Task RespondHeartbeat()
    {
        await Task.CompletedTask;
    }

    public async Task RequestVotes(int candidateId)
    {
        foreach (INode _node in Nodes)
        {
            await _node.ReceiveRequestVote(candidateId);
        }
    }

    public async Task ReceiveRequestVote(int candidateId)
    {
        var candidate = Nodes.Find(node => node.Id == candidateId);
        if (candidate != null && candidate.Term >= Term && VotedTerm != candidate.Term)
        {
            VotedTerm = candidate.Term;
            await candidate.SendVote();
        }
    }

    public async Task SendVote()
    {
        Votes.Add(Term);
        CheckElection();
        await Task.CompletedTask;
    }

    public void ResetTimer()
    {
        Timer.Stop();
        int randomInterval = new Random().Next(MinInterval, MaxInterval);
        Timer.Interval = randomInterval;
        Timer.Start();
        StartTime = DateTime.Now;
    }

    private void OnElectionTimeout(object? sender, ElapsedEventArgs e)
    {
        switch (State)
        {
            case NodeState.Follower:
                BecomeCandidate();
                break;
            case NodeState.Candidate:
                BecomeCandidate();
                break;
        }

    }

    public async void BecomeCandidate()
    {
        State = NodeState.Candidate;
        Term += 1;
        Votes.Clear();
        Votes.Add(Term);
        VotedTerm = Term;
        ResetTimer();
        await RequestVotes(Id);
    }

    public void CheckElection()
    {
        int votesReceived = Votes.Count(entry => entry == Term);
        int majorityNodes = (Nodes.Count + 1) / 2;

        if (votesReceived > majorityNodes && votesReceived > 1)
        {
            BecomeLeader();
        }
        else
        {
            BecomeCandidate();
        }
    }

    public async void BecomeLeader()
    {
        State = NodeState.Leader;
        LeaderId = Id;
        await SendHeartbeat();
    }

    public double TimerElapsed()
    {
        TimeSpan timePassed = DateTime.Now - StartTime;
        ElapsedTime = timePassed.TotalSeconds * 100;
        return ElapsedTime;
    }
}
