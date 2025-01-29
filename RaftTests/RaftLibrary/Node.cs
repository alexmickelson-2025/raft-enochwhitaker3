using System.Timers;
using System.Xml.Linq;

namespace RaftLibrary;

public class Node
{
    public int Id { get; set; }
    public int LeaderId { get; set; }
    public int VotedTerm { get; set; }
    public int Term { get; set; }
    public int AppendedEntry { get; set; }
    public NodeState State { get; set; }
    public bool IsRunning { get; set; }
    public int MinInterval { get; set; }
    public int MaxInterval { get; set; }
    public int LeaderInterval { get; set; }
    public int? CommittedIndex { get; set; }
    public int NextIndex { get; set; }
    public int CommittedResponseCount { get; set; }
    public System.Timers.Timer Timer { get; set; }
    public DateTime StartTime { get; set; }
    public double ElapsedTime { get; set; }
    public List<int> Votes { get; set; }
    public List<INode> Nodes { get; set; }
    public List<Entry> Log { get; set; }
    public Dictionary<int, string> StateMachine { get; set; }
    public Dictionary<int, int> OtherNextIndexes { get; set; }
    public IClient Client { get; set; }

    public Node(List<INode>? nodes = null, int? minInterval = null, int? maxInterval = null, int? leaderInterval = null, IClient? client = null)
    {
        Id = new Random().Next(1, 10000);
        State = NodeState.Follower;
        IsRunning = true;
        Votes = [];
        Nodes = nodes ?? [];
        Log = [];
        StateMachine = [];
        OtherNextIndexes = [];
        MinInterval = minInterval ?? 150;
        MaxInterval = maxInterval ?? 300;
        LeaderInterval = leaderInterval ?? 50;
        CommittedIndex = null;
        int randomInterval = new Random().Next(MinInterval, MaxInterval);
        Timer = new System.Timers.Timer(randomInterval);
        Timer.Elapsed += OnElectionTimeout;
        Timer.AutoReset = false;
        Timer.Start();
        StartTime = DateTime.Now;
        Client = client ?? new Client();
    }

    public async Task SendHeartbeat()
    {
        StartTime = DateTime.Now;

        foreach (INode _node in Nodes)
        {
            int _nodeNextIndex = OtherNextIndexes[_node.Id]; //where the leader thinks the node's next index is
            int differenceInLogs = Log.Count - _nodeNextIndex; //seeing if it should receive log
            if (differenceInLogs > 0) //if difference in log is greater than 0, then there is a log to be sent 
            {
                List<Entry> newEntryList = Log.TakeLast(differenceInLogs).ToList();
                await _node.ReceiveHeartbeat(Term, Id, Log.Count - 1, Log[^1].Term, CommittedIndex, newEntryList);
            }
            else if (differenceInLogs == 0)
            {
                if (Log.Count == 0) //handling if there hasn't been a single log yet 
                    await _node.ReceiveHeartbeat(Term, Id, null, null, null);
                else await _node.ReceiveHeartbeat(Term, Id, Log.Count - 1, Log[^1].Term, CommittedIndex);
            }
        }
        StartLeaderTimer();
    }


    public async Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId, int? prevLogIndex, int? prevLogTerm, int? leadersCommitIndex, List<Entry>? newEntries = null)
    {
        var leader = Nodes.Find(node => node.Id == receivedLeaderId);

        if (receivedTermId >= Term && leader != null && IsRunning == true)
        {
            if (State == NodeState.Leader && receivedTermId == Term)
                await Task.CompletedTask;

            bool canAccept = CheckHeartbeat(prevLogIndex, prevLogTerm);

            if(canAccept == true) 
            {
                if (newEntries != null)
                {
                    foreach (var log in newEntries)
                    {
                        Log.Add(log);
                    }
                    BecomeFollower(receivedLeaderId);
                    await leader.RespondHeartbeat(Id, Term, Log.Count == 0 ? null : Log.Count - 1, true, true);
                }
                else if (leadersCommitIndex > CommittedIndex || CommittedIndex == null && leadersCommitIndex != null)
                {
                    int bars = (int)leadersCommitIndex;
                    Entry kms = Log[bars];
                    CommitToStateMachine(kms);
                    BecomeFollower(receivedLeaderId);
                    await leader.RespondHeartbeat(Id, Term, Log.Count == 0 ? null : Log.Count - 1, true);
                }
                else
                {
                    BecomeFollower(receivedLeaderId);
                    await leader.RespondHeartbeat(Id, Term, Log.Count == 0 ? null : Log.Count - 1, true);
                }
            }
            else
            {
                BecomeFollower(receivedLeaderId);
                await leader.RespondHeartbeat(Id, Term, Log.Count == 0 ? null : Log.Count - 1, false);
            }
        }
    }

    public bool CheckHeartbeat(int? prevLogIndex, int? prevLogTerm)
    {
        if(prevLogIndex != null && prevLogTerm != null)
        {
            if(Log.Count == 0)
            {
                return true;
            }
            else if (prevLogIndex >= 0 && prevLogIndex == Log.Count - 1)
            {
                if (Log[(int)prevLogIndex].Term == prevLogTerm)
                {
                    return true;
                }
                return false;
            }
            else if (Log.Count > prevLogIndex)
            {
                int n = (Log.Count - 1) - (int)prevLogIndex;
                EditLog(n);
                return false; //this might cause problems in the future
            }
            return false;
        }
        return true; //this ALSO might cause problems killin myself if it does lowkey
    }

    public async Task RespondHeartbeat(int followerId, int term, int? logIndex, bool acceptedRPC, bool? addedToLog = null)
    {
        if (addedToLog != null && addedToLog == true)
        {
            CommittedResponseCount += 1;
            CheckCommits();
        }
        else if (acceptedRPC == false && logIndex < Log.Count - 1)
        {
            OtherNextIndexes[followerId]--;
        }
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
        if (candidate != null && candidate.Term >= Term && VotedTerm != candidate.Term && IsRunning == true)
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

    public void StartLeaderTimer()
    {
        Timer.Stop();
        Timer.Dispose();
        Timer = new System.Timers.Timer(LeaderInterval);
        Timer.Elapsed += async (sender, e) => await SendHeartbeat();
        Timer.AutoReset = false;
        Timer.Start();
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

    public async void BecomeFollower(int leaderId)
    {
        var leader = Nodes.Find(node => node.Id == leaderId);
        if ( leader != null)
        {
            State = NodeState.Follower;
            LeaderId = leader.Id;
            Term = leader.Term;
            Votes.Clear();
            ResetTimer();
        }
        await Task.CompletedTask;
    }

    public async void BecomeCandidate()
    {
        State = NodeState.Candidate;
        Term += 1;
        Votes.Add(Term);
        VotedTerm = Term;
        ResetTimer();
        await RequestVotes(Id);
    }

    public async void BecomeLeader()
    {
        State = NodeState.Leader;
        LeaderId = Id;
        foreach (INode _node in Nodes)
        {
            if (!OtherNextIndexes.ContainsKey(_node.Id))
            {
                OtherNextIndexes.TryAdd(_node.Id, Log.Count); //it is log.count for seperation
            }
        }
        await SendHeartbeat();
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

    public void CheckCommits()
    {
        int majorityNodes = (Nodes.Count + 1) / 2;

        if (CommittedResponseCount > majorityNodes && CommittedResponseCount > 1)
        {
            Entry entryToCommit = Log.Last();
            CommitToStateMachine(entryToCommit);
        }
        else
        {
            return;
        }
    }

    public double TimerElapsed()
    {
        TimeSpan timePassed = DateTime.Now - StartTime;
        ElapsedTime = timePassed.TotalSeconds * 100;
        return ElapsedTime;
    }

    public void ReceiveClientCommand(int requestedKey, string requestedCommand)
    {
        Entry newEntry = new(requestedKey, requestedCommand, Term);
        Log.Add(newEntry);
    }

    public void EditLog(int removeAmount)
    {
        if (removeAmount > 0 && removeAmount <= Log.Count)
        {
            Log.RemoveRange(Log.Count - removeAmount, removeAmount);
        }
    }

    public void CommitToStateMachine(Entry entry)
    {
        CommittedResponseCount = 0;
        StateMachine.Add(entry.Key, entry.Command);
        Client.hasCommittedCommand(entry.Key);
        if (CommittedIndex == null)
            CommittedIndex = 0;
        else CommittedIndex += 1;
    }

    public void TogglePause(bool pause)
    {
        if (pause == true)
        {
            Timer.Stop();
            IsRunning = false;
        }
        if (pause == false)
        {
            if (State == NodeState.Leader)
            {
                IsRunning = true;
                StartLeaderTimer();
            }
            else
            {
                IsRunning = true;
                ResetTimer();
            }
        }
    }
}
