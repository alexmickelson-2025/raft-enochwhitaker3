﻿using System.Timers;
using System.Xml.Linq;
using static RaftLibrary.DTOs;

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
                var heartbeatData = new ReceiveHeartbeatDTO
                {
                    receivedTermId = Term,
                    receivedLeaderId = Id,
                    prevLogIndex = OtherNextIndexes[_node.Id] != 0 ? OtherNextIndexes[_node.Id ] - 1 : null,
                    prevLogTerm = OtherNextIndexes[_node.Id] != 0 ? Log[OtherNextIndexes[_node.Id] - 1].Term : null,
                    leadersCommitIndex = CommittedIndex,
                    newEntries = newEntryList
                };
                await _node.ReceiveHeartbeat(heartbeatData);
            }
            else if (differenceInLogs == 0)
            {
                var heartbeatData = new ReceiveHeartbeatDTO
                {
                    receivedTermId = Term,
                    receivedLeaderId = Id,
                    prevLogIndex = Log.Count > 0 ? Log.Count - 1 : null,
                    prevLogTerm = Log.Count > 0 ? Log[^1].Term : null,
                    leadersCommitIndex = CommittedIndex,
                    newEntries = null
                };
                await _node.ReceiveHeartbeat(heartbeatData);
            }
        }
        StartLeaderTimer();
    }


    public async Task ReceiveHeartbeat(ReceiveHeartbeatDTO Data)
    {
        var leader = Nodes.Find(node => node.Id == Data.receivedLeaderId);
        if (Data.receivedTermId >= Term && leader != null && IsRunning == true)
        {
            if(Data.receivedTermId > Term && State == NodeState.Candidate)
            {
                BecomeFollower(Data.receivedLeaderId, Data.receivedTermId);
                await Task.CompletedTask;
            }

            if (State == NodeState.Leader && Data.receivedTermId == Term)
                await Task.CompletedTask;

            bool canAccept = CheckHeartbeat(Data.prevLogIndex, Data.prevLogTerm);

            if(canAccept == true) 
            {
                if (Data.newEntries != null)
                {
                    foreach (var log in Data.newEntries)
                    {
                        Log.Add(log);
                    }
                    BecomeFollower(Data.receivedLeaderId, Data.receivedTermId);
                    var responseData = new RespondHeartbeatDTO
                    {
                        id = Id,
                        term = Term,
                        logIndex = Log.Count == 0 ? null : Log.Count - 1,
                        acceptedRPC = true,
                        addedToLog = true
                    };
                    await leader.RespondHeartbeat(responseData);
                }
                else if (Data.leadersCommitIndex > CommittedIndex || CommittedIndex == null && Data.leadersCommitIndex != null)
                {
                    if((int)Data.leadersCommitIndex == 0)
                    {
                        int bars = (int)Data.leadersCommitIndex;
                        Entry kms = Log[bars];
                        await CommitToStateMachine(kms);
                    }
                    else
                    {
                        if(CommittedIndex != null)
                        {

                            int toCommit = (int)Data.leadersCommitIndex - (int)CommittedIndex;
                            var newEntriesToCommit = Log.TakeLast(toCommit);
                            foreach (var entry in newEntriesToCommit)
                            {
                                await CommitToStateMachine(entry);
                            }
                        }
                        else
                        {
                            var newEntriesToCommit = Log.TakeLast((int)Data.leadersCommitIndex);
                            foreach (var entry in newEntriesToCommit)
                            {
                                await CommitToStateMachine(entry);
                            }
                        }
                    }

                    var responseData = new RespondHeartbeatDTO
                    {
                        id = Id,
                        term = Term,
                        logIndex = Log.Count == 0 ? null : Log.Count - 1,
                        acceptedRPC = true,
                    };
                    BecomeFollower(Data.receivedLeaderId, Data.receivedTermId);
                    await leader.RespondHeartbeat(responseData);
                }
                else
                {
                    var responseData = new RespondHeartbeatDTO
                    {
                        id = Id,
                        term = Term,
                        logIndex = Log.Count == 0 ? null : Log.Count - 1,
                        acceptedRPC = true
                    };
                    BecomeFollower(Data.receivedLeaderId, Data.receivedTermId);
                    await leader.RespondHeartbeat(responseData);
                }
            }
            else
            {
                var responseData = new RespondHeartbeatDTO
                {
                    id = Id,
                    term = Data.receivedTermId,
                    logIndex = Log.Count == 0 ? null : Log.Count - 1,
                    acceptedRPC = false
                };
                BecomeFollower(Data.receivedLeaderId, Data.receivedTermId);
                await leader.RespondHeartbeat(responseData);
            }
        }
    }

    public bool CheckHeartbeat(int? prevLogIndex, int? prevLogTerm) //2, 1
    {
        if(prevLogIndex != null && prevLogTerm != null)
        {
            if(Log.Count == 0)
            {
                return true;
            }
            else if (prevLogIndex >= 0 && prevLogIndex == Log.Count) //2 == 1 NOPE
            {
                if (Log[(int)prevLogIndex - 1].Term == prevLogTerm)
                {
                    return true;
                }
                return false;
            }
            else if (prevLogIndex == Log.Count - 1)
            {
                if (Log[(int)prevLogIndex].Term == prevLogTerm)
                {
                    return true;
                }
            }
            else if (Log.Count > prevLogIndex) // 1 > 1 false
            {
                int n = (Log.Count - 1) - (int)prevLogIndex;
                EditLog(n);
                return false; //this might cause problems in the future
            }
            return false;
        }
        return true; //this ALSO might cause problems killin myself if it does lowkey
    }

    public async Task RespondHeartbeat(RespondHeartbeatDTO Data)
    {
        if (Data.addedToLog != null && Data.addedToLog == true && Data.logIndex != null)
        {
            OtherNextIndexes[Data.id] = Log.Count;
            await CheckCommits();
        }
        else if (Data.acceptedRPC == false && Data.logIndex < Log.Count - 1)
        {
            OtherNextIndexes[Data.id]--;
        }
        await Task.CompletedTask;
    }

    public async Task RequestVotes(RequestVoteDTO Data)
    {
        foreach (INode _node in Nodes)
        {
            var requestData = new ReceiveRequestVoteDTO
            {
                candidateId = Id,
                candidateTerm = Term,
            };
            await _node.ReceiveRequestVote(requestData);
        }
    }

    public async Task ReceiveRequestVote(ReceiveRequestVoteDTO Data)
    {
        var candidate = Nodes.Find(node => node.Id == Data.candidateId);
        if (candidate != null && Data.candidateTerm >= Term && VotedTerm != Data.candidateTerm && IsRunning == true)
        {
            VotedTerm = Data.candidateTerm;
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

    public void StartTimer()
    {
        IsRunning = false;
        Timer.Start();
    }

    public async void BecomeFollower(int leaderId, int leaderTerm)
    {
        var leader = Nodes.Find(node => node.Id == leaderId);
        if ( leader != null)
        {
            State = NodeState.Follower;
            LeaderId = leader.Id;
            Term = leaderTerm;
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
        var requestData = new RequestVoteDTO
        {
            Id = Id,
            Term = Term,
        };
        await RequestVotes(requestData);
    }

    public async void BecomeLeader()
    {
        State = NodeState.Leader;
        LeaderId = Id;
        foreach (INode _node in Nodes)
        {
            if (!OtherNextIndexes.ContainsKey(_node.Id))
            {
                OtherNextIndexes.TryAdd(_node.Id, Log.Count); //it is log.count for next node index
            }
            else
            {
                OtherNextIndexes[_node.Id] = Log.Count;
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

    public async Task CheckCommits()
    {
        int majorityNodes = (Nodes.Count + 1) / 2;
        List<int> countOfLogged = [];

        foreach(INode _node in Nodes) 
        {
            if (OtherNextIndexes[_node.Id] > StateMachine.Count) //3 > 1 => 3-1 =2 => 2
            {
                int uncommittedLogs = OtherNextIndexes[_node.Id] - StateMachine.Count;
                countOfLogged.Add(uncommittedLogs);
            }
        }


        if (countOfLogged.Count + 1 > majorityNodes)
        {
            int commitAmount = countOfLogged.Min();
            var newEntriesToCommit = Log.TakeLast(commitAmount);
            foreach (var entry in newEntriesToCommit)
            {
                await CommitToStateMachine(entry);
            }
            countOfLogged.Clear();
        }

        else
        {
            await Task.CompletedTask;
        }
    }

    public double TimerElapsed()
    {
        TimeSpan timePassed = DateTime.Now - StartTime;
        ElapsedTime = timePassed.TotalSeconds * 100;
        return ElapsedTime;
    }

    public async Task ReceiveClientCommand(ClientCommandData Data)
    {
        if (State != NodeState.Leader)
        {
            await Client.appendResponses($"Node {Id} is not a leader");
            await Task.CompletedTask;
        }
        else
        {
            Client.LeaderId = Id;
            Entry newEntry = new(Data.requestedKey, Data.requestedCommand, Term);
            Log.Add(newEntry);
        };
    }

    public void EditLog(int removeAmount)
    {
        if (removeAmount > 0 && removeAmount <= Log.Count)
        {
            Log.RemoveRange(Log.Count - removeAmount, removeAmount);
        }
    }

    public async Task CommitToStateMachine(Entry entry)
    {
        StateMachine.Add(entry.Key, entry.Command);
        if (State == NodeState.Leader)
            await Client.hasCommittedCommand(entry.Key, entry.Command);
        if (CommittedIndex == null)
            CommittedIndex = 0;
        else CommittedIndex += 1;
    }

    public async Task TogglePause(bool pause)
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
        await Task.CompletedTask;
    }
}
