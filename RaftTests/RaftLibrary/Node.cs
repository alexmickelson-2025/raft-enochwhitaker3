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

    public Node(List<INode>? nodes = null, int? minInterval = null, int? maxInterval = null, int? leaderInterval = null)
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
    }

    public async Task SendHeartbeat()
    {
        StartTime = DateTime.Now;

        foreach (INode _node in Nodes.Where(x => x.IsRunning == true))
        {
            bool didSucceed = CheckHeartbeat(_node);

            if (didSucceed)
            {
                int _nodeNextIndex = OtherNextIndexes[_node.Id];
                int differenceInLogs = Log.Count - _nodeNextIndex;
                if (differenceInLogs > 0)
                {
                    List<Entry> newEntryList = Log.TakeLast(differenceInLogs).ToList();
                    await _node.ReceiveHeartbeat(Term, Id, CommittedIndex, Log.Count - 1, Log[^1].Term, newEntryList);
                }
                else if (differenceInLogs == 0)
                {
                    if (Log.Count == 0)
                        await _node.ReceiveHeartbeat(Term, Id, CommittedIndex, 0, Term);
                    else await _node.ReceiveHeartbeat(Term, Id, CommittedIndex, Log.Count - 1, Log[^1].Term);
                }
            }
            else
            {
                await RespondHeartbeat(_node.Id, _node.Term, _node.Log.Count - 1, false);
            }
        }
        StartLeaderTimer();
    }

    public bool CheckHeartbeat(INode _node) //probably gonna cause issues on kubernetes since I am directly calling node
    {
        int? prevLogIndex;
        int? prevLogTerm;
        if (OtherNextIndexes[_node.Id] == 0)
        {
            prevLogIndex = null;
            prevLogTerm = null;
        }
        else
        {
            prevLogIndex = OtherNextIndexes[_node.Id] - 1;
            prevLogTerm = Log[^1].Term;
        }

        if (prevLogIndex >= 0 && prevLogIndex == _node.Log.Count - 1)
        {
            if (_node.Log[(int)prevLogIndex].Term == prevLogTerm)
            {
                return true;
            }
            return false;
        }
        else if (_node.Log.Count == 0 && Log.Count <= 1)
        {
            OtherNextIndexes[_node.Id] = 0;
            return true;
        }
        else if (_node.Log.Count > prevLogIndex)
        {
            int n = (_node.Log.Count - 1) - (int)prevLogIndex;
            _node.EditLog(n);
            return false; //this might cause problems in the future
        }
        return false;
    }

    public async Task ReceiveHeartbeat(int receivedTermId, int receivedLeaderId, int? leadersCommitIndex, int prevLogIndex, int prevLogTerm, List<Entry>? newEntries = null)
    {
        var leader = Nodes.Find(node => node.Id == receivedLeaderId);

        if (receivedTermId >= Term && leader != null)
        {
            if (State == NodeState.Leader && receivedTermId == Term)
                await Task.CompletedTask;

            else
            {
                if (newEntries != null)
                {
                    foreach (var log in newEntries)
                    {
                        Log.Add(log);
                    }
                    BecomeFollower(leader, true);
                }
                else if (leadersCommitIndex > CommittedIndex || CommittedIndex == null && leadersCommitIndex != null)
                {
                    int bars = (int)leadersCommitIndex;
                    Entry kms = Log[bars];
                    CommitToStateMachine(kms);
                    BecomeFollower(leader, true);
                }
                else
                {
                    BecomeFollower(leader, true);
                }
            }
        }
    }

    public async Task RespondHeartbeat(int id, int term, int logIndex, bool result, bool? addedToLog = null)
    {
        if (addedToLog != null && addedToLog == true)
        {
            CommittedResponseCount += 1;
            CheckCommits();
        }
        else if (result == false)
        {
            OtherNextIndexes[id]--;
        }
        await Task.CompletedTask;
    }

    public async Task RequestVotes(int candidateId)
    {
        foreach (INode _node in Nodes.Where(x => x.IsRunning == true))
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

    public async void BecomeFollower(INode leader, bool success, bool? addedToLog = null)
    {
        State = NodeState.Follower;
        LeaderId = leader.Id;
        Term = leader.Term;
        Votes.Clear();
        ResetTimer();
        if (addedToLog != null && addedToLog == true && success == true)
            await leader.RespondHeartbeat(Id, Term, Log.Count - 1, success, addedToLog);
        else await leader.RespondHeartbeat(Id, Term, Log.Count - 1, success, null);
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
