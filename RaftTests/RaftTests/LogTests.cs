using FluentAssertions;
using NSubstitute;
using RaftLibrary;
using static RaftLibrary.DTOs;

namespace RaftTests;
public class LogTests
{
    public bool AreObjectsEquivalent(object obj1, object obj2)
    {
        try
        {
            obj1.Should().BeEquivalentTo(obj2);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Test 1
    [Fact]
    public async Task When_Leader_Receives_ClientCommand_It_Sends_Log_Entry_In_Next_RPC()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        var node = new Node([fauxNode]) { State = NodeState.Leader };
        fauxNode.Id = node.Id + 1;
        node.OtherNextIndexes.Add(fauxNode.Id, node.Log.Count);

        // Act
        Entry newEntry = new(1, "Command1", 0);
        node.ReceiveClientCommand(1, "Command1");
        await node.SendHeartbeat();

        // Assert
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = node.Id,
            prevLogIndex = node.Log.Count - 1,
            prevLogTerm = node.Log[^1].Term,
            leadersCommitIndex = node.CommittedIndex,
            newEntries = [newEntry]
        };

       await fauxNode.Received(1).ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
            data.receivedTermId == heartbeatData.receivedTermId
            && data.receivedLeaderId == heartbeatData.receivedLeaderId
            && data.prevLogIndex == heartbeatData.prevLogIndex
            && data.prevLogTerm == heartbeatData.prevLogTerm
            && data.leadersCommitIndex == heartbeatData.leadersCommitIndex
            && data.newEntries != null
            && AreObjectsEquivalent(data.newEntries, heartbeatData.newEntries)
        ));
    }

    // Test 2
    [Fact]
    public void When_Leader_Receives_ClientCommand_It_Appends_To_Its_Log()
    {
        // Arrange
        var node = new Node() { State = NodeState.Leader };
        Entry expectedEntry = new(12345, "12345", node.Term);

        // Act
        node.ReceiveClientCommand(12345, "12345");

        // Assert
        node.Log.Should().BeEquivalentTo([expectedEntry]);
    }

    // Test 3
    [Fact]
    public void When_A_Node_Is_New_Its_Log_Is_Empty()
    {
        // Arrange
        var node = new Node();

        // Assert
        node.Log.Count.Should().Be(0);
    }

    // Test 4
    [Fact]
    public void When_Leader_Wins_Election_It_Sets_NextIndex_For_Followers_To_Next_Last_Index()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        var node = new Node([fauxNode]);
        fauxNode.Id = node.Id + 1;

        // Act
        node.BecomeLeader();

        // Assert
        node.OtherNextIndexes.Count.Should().Be(1);
        node.OtherNextIndexes[fauxNode.Id] = node.Log.Count();
    }

    // Test 5
    [Fact]
    public async Task Leaders_Maintain_NextIndex_ForEach_Follower_That_Matches_The_Next_Log_It_Will_Send()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        var fauxNode2 = Substitute.For<INode>();
        var node = new Node([fauxNode, fauxNode2]);
        fauxNode.Id = node.Id + 1;
        fauxNode2.Id = node.Id + 2;

        node.Log = [
                new Entry(1,"Command1", node.Term), // 0 
                new Entry(2, "Command2", node.Term), // 1
                new Entry(3, "Command3", node.Term), // 2
                new Entry(4, "Command4", node.Term)  // INDEX 3, COUNT = 4, NEXT INDEX = 4
            ];

        node.OtherNextIndexes[fauxNode.Id] = 3;
        node.OtherNextIndexes[fauxNode2.Id] = 1;

        // Act
        await node.SendHeartbeat();
        await Task.Delay(200);

        // Assert
        var list1 = new List<Entry>
        {
            new Entry(4, "Command4", node.Term)
        };
        var list2 = new List<Entry>
        {
            new Entry(2, "Command2", node.Term), 
            new Entry(3, "Command3", node.Term), 
            new Entry(4, "Command4", node.Term)
        };
        var heartbeatData1 = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = node.Id,
            prevLogIndex = node.Log.Count - 1,
            prevLogTerm = node.Log[^1].Term,
            leadersCommitIndex = node.CommittedIndex,
            newEntries = list1
        };
        var heartbeatData2 = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = node.Id,
            prevLogIndex = node.Log.Count - 1,
            prevLogTerm = node.Log[^1].Term,
            leadersCommitIndex = node.CommittedIndex,
            newEntries = list2
        };

        await fauxNode.Received().ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
             data.receivedTermId == heartbeatData1.receivedTermId
             && data.receivedLeaderId == heartbeatData1.receivedLeaderId
             && data.prevLogIndex == heartbeatData1.prevLogIndex
             && data.prevLogTerm == heartbeatData1.prevLogTerm
             && data.leadersCommitIndex == heartbeatData1.leadersCommitIndex
             && data.newEntries != null
             && AreObjectsEquivalent(data.newEntries, heartbeatData1.newEntries)
         ));

        await fauxNode2.Received().ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
            data.receivedTermId == heartbeatData2.receivedTermId
            && data.receivedLeaderId == heartbeatData2.receivedLeaderId
            && data.prevLogIndex == heartbeatData2.prevLogIndex
            && data.prevLogTerm == heartbeatData2.prevLogTerm
            && data.leadersCommitIndex == heartbeatData2.leadersCommitIndex
            && data.newEntries != null
            && AreObjectsEquivalent(data.newEntries, heartbeatData2.newEntries)
        ));
    }

    // Test 6
    [Fact]
    public async Task Highest_Committed_Index_From_Leader_Is_Included_In_AppendEntries_RPC()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        var node = new Node([fauxNode]);
        fauxNode.Id = node.Id + 1;
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];

        // Act
        node.BecomeLeader();

        // Assert
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = node.Id,
            prevLogIndex = node.Log.Count - 1,
            prevLogTerm = node.Log[^1].Term,
            leadersCommitIndex = node.CommittedIndex
        };
        await fauxNode.Received().ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
            data.receivedTermId == heartbeatData.receivedTermId
            && data.receivedLeaderId == heartbeatData.receivedLeaderId
            && data.prevLogIndex == heartbeatData.prevLogIndex
            && data.prevLogTerm == heartbeatData.prevLogTerm
            && data.leadersCommitIndex == heartbeatData.leadersCommitIndex
        ));
    }

    // Test 7
    [Fact]
    public async Task When_Follower_Learns_Log_Is_Committed_It_Too_Commits_Log()
    {
        // Arrange
        var fauxLeaderNode = Substitute.For<INode>();
        var node = new Node([fauxLeaderNode]);
        int leaderTerm = node.Term;
        fauxLeaderNode.Id = node.Id + 1;
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = leaderTerm,
            receivedLeaderId = fauxLeaderNode.Id,
            prevLogIndex = 0,
            prevLogTerm = leaderTerm,
            leadersCommitIndex = 0
        };
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        node.StateMachine.Should().Contain(1, "Command1");
    }

    // Test 8
    [Fact]
    public void Leader_Commits_Log_After_Receiving_Majority_Response()
    {
        //Arrange
        var fauxNode = Substitute.For<INode>();
        var fauxNode2 = Substitute.For<INode>();
        var node = new Node([fauxNode, fauxNode2]);
        fauxNode.Id = node.Id + 1;
        fauxNode2.Id = node.Id + 2;
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];
        node.CommittedResponseCount = 2;

        // Act
        node.CheckCommits();

        // Assert
        node.StateMachine.Should().Contain(1, "Command1");
    }

    // Test 9
    [Fact]
    public void Leader_Commits_Log_By_Incrementing_CommittedLogIndex()
    {
        //Arrange
        var fauxNode = Substitute.For<INode>();
        var fauxNode2 = Substitute.For<INode>();
        var node = new Node([fauxNode, fauxNode2]);
        fauxNode.Id = node.Id + 1;
        fauxNode2.Id = node.Id + 2;
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];
        node.CommittedResponseCount = 2;

        // Act
        node.CheckCommits();

        // Assert
        node.CommittedIndex.Should().Be(0);
    }

    // Test 10
    [Fact]
    public async Task When_Follower_Receives_Logs_It_Adds_Them_To_Personal_Log()
    {
        // Arrange
        var fauxLeaderNode = Substitute.For<INode>();
        var node = new Node([fauxLeaderNode]);
        int leaderTerm = 1;
        fauxLeaderNode.Id = node.Id + 1;
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = leaderTerm,
            receivedLeaderId = fauxLeaderNode.Id,
            prevLogIndex = 0,
            prevLogTerm = leaderTerm,
            leadersCommitIndex = null
        };
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        node.Log.Should().Contain(entry);
    }

    // Test 11
    [Fact]
    public async Task Followers_Response_To_AppendEntriesRPC_Includes_Term_And_LogEntryIndex()
    {
        // Arrange
        var fauxLeaderNode = Substitute.For<INode>();
        var node = new Node([fauxLeaderNode]);
        int leaderTerm = node.Term;
        fauxLeaderNode.Id = node.Id + 1;
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = leaderTerm,
            receivedLeaderId = fauxLeaderNode.Id,
            prevLogIndex = 0,
            prevLogTerm = leaderTerm,
            leadersCommitIndex = null
        };
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        var responseData = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = node.Log.Count - 1, //IDK IF THIS IS RIGHT
            acceptedRPC = true,
        };
        await fauxLeaderNode.Received(1).RespondHeartbeat(Arg.Is<RespondHeartbeatDTO>(data =>
            data.id == responseData.id
            && data.term == responseData.term
            && data.logIndex == responseData.logIndex
            && data.acceptedRPC == responseData.acceptedRPC
        ));
    }


    // Test 13
    [Fact]
    public void Leader_Commits_Log_To_State_Machine()
    {
        //Arrange
        var node = new Node();
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];

        // Act
        node.CommitToStateMachine(entry);

        // Assert
        node.StateMachine.Should().Contain(1, "Command1");
    }

    // Test 14
    [Fact]
    public async Task When_Follower_Receives_Heartbeat_It_Should_Adjust_CommitIndex_To_LeadersCommitIndex()
    {
        //Arrange
        var fauxLeaderNode = Substitute.For<INode>();
        int leaderTerm = 1;
        var node = new Node([fauxLeaderNode]);
        fauxLeaderNode.Id = node.Id + 1;
        Entry entry = new(1, "Command1", leaderTerm);
        node.Log = [entry];

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = leaderTerm,
            receivedLeaderId = fauxLeaderNode.Id,
            prevLogIndex = 0,
            prevLogTerm = leaderTerm,
            leadersCommitIndex = 0
        };
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        node.CommittedIndex.Should().Be(0);
    }

    // Test 15a
    [Fact]
    public async Task When_Sending_AppendEntries_It_Sends_PrevLogIndex_And_PrevLogTerm()
    {
        //Arrange
        var fauxNode = Substitute.For<INode>();
        var node = new Node([fauxNode]);
        fauxNode.Id = node.Id + 1;
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];

        // Act
        node.BecomeLeader();

        // Assert
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = node.Id,
            prevLogIndex = node.Log.Count - 1,
            prevLogTerm = node.Log[^1].Term,
            leadersCommitIndex = node.CommittedIndex
        };

        await fauxNode.Received(1).ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
             data.receivedTermId == heartbeatData.receivedTermId
             && data.receivedLeaderId == heartbeatData.receivedLeaderId
             && data.prevLogIndex == heartbeatData.prevLogIndex
             && data.prevLogTerm == heartbeatData.prevLogTerm
             && data.leadersCommitIndex == heartbeatData.leadersCommitIndex
         ));

    }

    // Test 15b1
    [Fact]
    public async Task If_Follower_Log_DoesNot_Contain_PreLogIndex_It_Rejects()
    {
        //Arrange
        Entry leaderEntry = new(1, "Command1", 1);
        var fauxNode = Substitute.For<INode>();
        var node = new Node([fauxNode]);
        node.Log = [leaderEntry];
        fauxNode.Id = node.Id + 1;

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = 1,
            receivedLeaderId = fauxNode.Id,
            prevLogIndex = 2,
            prevLogTerm = 1,
            leadersCommitIndex = 1
        };
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        var responseData = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = node.Log.Count - 1,
            acceptedRPC = false,
        };
        await fauxNode.Received().RespondHeartbeat(Arg.Is<RespondHeartbeatDTO>(data =>
            data.id == responseData.id
            && data.term == responseData.term
            && data.logIndex == responseData.logIndex
            && data.acceptedRPC == responseData.acceptedRPC
        ));
    }

    // Test 15b2
    [Fact]
    public async Task If_Follower_Log_Does_Contain_PreLogIndex_But_PreLogTerm_DoesNot_Match_It_Rejects()
    {
        //Arrange
        Entry leaderEntry = new(1, "Command1", 1);
        var fauxNode = Substitute.For<INode>();
        fauxNode.Id = 2;
        var node = new Node([fauxNode]);
        node.Id = 1;
        node.Log = [leaderEntry];

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = 1,
            receivedLeaderId = fauxNode.Id,
            prevLogIndex = 0,
            prevLogTerm = 2,
            leadersCommitIndex = 1
        };
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        var responseData = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = node.Log.Count - 1,
            acceptedRPC = false,
        };
        await fauxNode.Received().RespondHeartbeat(Arg.Is<RespondHeartbeatDTO>(data =>
            data.id == responseData.id
            && data.term == responseData.term
            && data.logIndex == responseData.logIndex
            && data.acceptedRPC == responseData.acceptedRPC
        ));
    }

    // Test 15d
    [Fact]
    public async Task If_AppendEntries_RPC_Log_Index_Is_Greater_Than_Followers_Index_It_Decreases()
    {
        //Arrange
        var fauxNode = Substitute.For<INode>();
        var node = new Node([fauxNode]);
        fauxNode.Id = node.Id + 1;
        int fauxNodeTerm = node.Term;
        Entry leaderEntry = new(1, "Command1", node.Term);
        Entry leaderEntry2 = new(2, "Command2", node.Term);
        Entry leaderEntry3 = new(3, "Command3", node.Term);
        Entry followerEntry = new(4, "Command1", node.Term);
        node.Log = [leaderEntry, leaderEntry2, leaderEntry3];
        node.OtherNextIndexes.Add(fauxNode.Id, node.Log.Count);
        int originalIndex = node.OtherNextIndexes[fauxNode.Id];

        // Act - This may seem weird but this is simulating fauxNodes response to node
        var responseData = new RespondHeartbeatDTO
        {
            id = fauxNode.Id,
            term = fauxNodeTerm,
            logIndex = 0,
            acceptedRPC = false,
        };
        await node.RespondHeartbeat(responseData);

        // Assert
        node.OtherNextIndexes[fauxNode.Id].Should().BeLessThan(originalIndex);
    }

    // Test 15e
    [Fact]
    public async Task If_AppendEntries_RPC_Log_Index_Is_Less_Than_Followers_Index_Follower_Deletes_Extra_Entries()
    {
        //Arrange
        var fauxNode = Substitute.For<INode>();
        var node = new Node([fauxNode]);
        fauxNode.Id = node.Id + 1;
        int fauxNodeTerm = node.Term;
        Entry followerEntry1 = new(2, "Command1", node.Term);
        Entry followerEntry2 = new(3, "Command2", node.Term); //This one should be gone
        Entry followerEntry3 = new(4, "Command3", node.Term); //This one should be gone
        node.Log = [followerEntry1, followerEntry2, followerEntry3];
        node.OtherNextIndexes.Add(fauxNode.Id, node.Log.Count);

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = fauxNodeTerm,
            receivedLeaderId = fauxNode.Id,
            prevLogIndex = 0,
            prevLogTerm = fauxNodeTerm,
            leadersCommitIndex = 0
        };
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        node.Log.Count.Should().Be(1);
    }

    // Test 15e2
    [Fact]
    public void Node_Edit_Log_Actually_Edits_Log()
    {
        //Arrange
        var node = new Node();
        Entry entry1 = new(1, "Command1", node.Term);
        Entry entry2 = new(2, "Command2", node.Term);
        Entry entry3 = new(3, "Command3", node.Term);
        node.Log = [entry1, entry2, entry3];

        // Act
        node.EditLog(2);

        // Assert
        node.Log.Count.Should().Be(1);
    }

    // Test 16
    [Fact]
    public async Task When_Leader_DoesNot_Get_Majority_Committed_The_Log_Remains_Uncommitted()
    {
        //Arrange
        var node = new Node();
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];

        // Act
        var responseDataTrue = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = node.Log.Count - 1,
            acceptedRPC = true,
        };
        var responseDataFalse = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = node.Log.Count - 1,
            acceptedRPC = false,
        };
        await node.RespondHeartbeat(responseDataTrue);
        await node.RespondHeartbeat(responseDataFalse);
        await node.RespondHeartbeat(responseDataFalse);
        await node.RespondHeartbeat(responseDataFalse);

        // Assert
        node.Log.Should().Contain(entry);
        node.StateMachine.Should().NotContain(1, "Command1");
    }


    // Test 17
    [Fact]
    public async Task When_Leader_Sends_Log_And_DoesNot_Get_Response_It_Still_Sends_Log_In_RPCs()
    {

        //Arrange
        var fauxNode = Substitute.For<INode>();
        var node = new Node([fauxNode]);
        node.IsRunning = false;
        Entry entry = new(1, "Command1", node.Term);
        List<Entry> dummy = [entry];
        fauxNode.Id = node.Id + 1;
        int fauxNodeTerm = node.Term;
        node.Log = [];

        //Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = fauxNodeTerm,
            receivedLeaderId = fauxNode.Id,
            prevLogIndex = 0,
            prevLogTerm = fauxNodeTerm,
            leadersCommitIndex = 0,
            newEntries = dummy
        };
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        var responseData1 = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = null,
            acceptedRPC = true,
        };
        var responseData2 = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = 0,
            acceptedRPC = true,
            addedToLog = true,
        };

        await fauxNode.Received(0).RespondHeartbeat(Arg.Is<RespondHeartbeatDTO>(data =>
            data.id == responseData1.id
            && data.term == responseData1.term
            && data.logIndex == responseData1.logIndex
            && data.acceptedRPC == responseData1.acceptedRPC
        ));

        node.IsRunning = true;
        await node.ReceiveHeartbeat(heartbeatData);

        await fauxNode.Received(1).RespondHeartbeat(Arg.Is<RespondHeartbeatDTO>(data =>
            data.id == responseData2.id
            && data.term == responseData2.term
            && data.logIndex == responseData2.logIndex
            && data.acceptedRPC == responseData2.acceptedRPC
        ));
    }

    // Test 18
    [Fact]
    public async Task When_Leader_Cannot_Commit_Entry_It_Does_Not_Send_Response_To_Client()
    {
        //Arrange
        var fauxClient = Substitute.For<IClient>();
        var node = new Node(null, null, null, null, fauxClient);
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];

        // Act
        var responseDataTT = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = node.Log.Count - 1,
            acceptedRPC = true,
            addedToLog = true
        };
        var responseDataTF = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = node.Log.Count - 1,
            acceptedRPC = true,
            addedToLog = false
        };
        await node.RespondHeartbeat(responseDataTT);
        await node.RespondHeartbeat(responseDataTF);
        await node.RespondHeartbeat(responseDataTF);
        await node.RespondHeartbeat(responseDataTF);

        // Assert
        node.Log.Should().Contain(entry);
        await fauxClient.Received(0).hasCommittedCommand(entry.Key);
    }

    // Test 19
    [Fact]
    public async Task If_Follower_Receives_Logs_That_Are_Too_Far_In_The_Future_It_Rejects_Them()
    {
        //Arrange
        Entry leaderEntry = new(1, "Command1", 1);
        var fauxNode = Substitute.For<INode>();
        int fauxNodeTerm = 10;
        var node = new Node([fauxNode]);
        node.Log = [leaderEntry];
        fauxNode.Id = node.Id + 1;

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = 1,
            receivedLeaderId = fauxNode.Id,
            prevLogIndex = 0,
            prevLogTerm = fauxNodeTerm,
            leadersCommitIndex = 1
        };
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        var responseData = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = node.Log.Count - 1,
            acceptedRPC = false,
        };
        await fauxNode.Received().RespondHeartbeat(Arg.Is<RespondHeartbeatDTO>(data =>
            data.id == responseData.id
            && data.term == responseData.term
            && data.logIndex == responseData.logIndex
            && data.acceptedRPC == responseData.acceptedRPC
        ));
    }

    //    //// Test 20
    //    //[Fact]
    //    //public async Task If_Follower_Receives_RPC_With_Term_And_Index_That_DoNot_Match_It_Retries_Until_It_Find_Ones()
    //    //{
    //    //    // Arrange
    //    //    var fauxNode = Substitute.For<INode>();
    //    //    var node = new Node([fauxNode]);
    //    //    fauxNode.Id = node.Id + 1;
    //    //    node.OtherNextIndexes.Add(fauxNode.Id, node.Log.Count);

    //    //    node.Log = [
    //    //            new Entry(1, "Command1", node.Term)
    //    //        ];

    //    //    fauxNode.Log = [
    //    //            new Entry(1,"Command1", node.Term),
    //    //            new Entry(2, "Command2", node.Term), 
    //    //            new Entry(3, "Command3", node.Term), 
    //    //            new Entry(4, "Command4", node.Term)  
    //    //        ];

    //    //    // Act
    //    //    await node.ReceiveHeartbeat(fauxNode.Term, fauxNode.Id, fauxNode.Log.Count - 1, fauxNode.Log[^1].Term, fauxNode.CommittedIndex, fauxNode.Log.TakeLast(1).ToList());
    //    //    await fauxNode.Received().RespondHeartbeat(node.Id, node.Term, node.Log.Count - 1, false);
    //    //    await node.ReceiveHeartbeat(fauxNode.Term, fauxNode.Id, fauxNode.Log.Count - 1, fauxNode.Log[^1].Term, fauxNode.CommittedIndex, fauxNode.Log.TakeLast(2).ToList());
    //    //    await fauxNode.Received().RespondHeartbeat(node.Id, node.Term, node.Log.Count - 1, false);
    //    //    await node.ReceiveHeartbeat(fauxNode.Term, fauxNode.Id, fauxNode.Log.Count - 1, fauxNode.Log[^1].Term, fauxNode.CommittedIndex, fauxNode.Log.TakeLast(3).ToList());
    //    //    await fauxNode.Received().RespondHeartbeat(node.Id, node.Term, node.Log.Count - 1, false);

    //    //}
}