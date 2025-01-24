using Castle.Core.Logging;
using FluentAssertions;
using NSubstitute;
using RaftLibrary;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System;
using Castle.Components.DictionaryAdapter.Xml;

namespace RaftTests;
public class LogTests
{
    // Test 1
    [Fact]
    public async Task When_Leader_Receives_ClientCommand_It_Sends_Log_Entry_In_Next_RPC()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        fauxNode.State = NodeState.Follower;
        var node = new Node([fauxNode]) { State = NodeState.Leader };
        fauxNode.Id = node.Id + 1;
        fauxNode.Log = [];
        node.OtherNextIndexes.Add(fauxNode.Id, node.Log.Count);

        // Act
        node.ReceiveClientCommand(12345, "12345");
        await node.SendHeartbeat();

        // Assert
        await fauxNode.Received().ReceiveHeartbeat(
            node.Term,
            node.Id,
            node.CommittedIndex,
            0,
            0,
            Arg.Is<List<Entry>>(entry =>
                entry != null && entry[0].Command == "12345" && entry[0].Term == node.Term)
        );
    }

    // Test 2
    [Fact]
    public void When_Leader_Receives_ClientCommand_It_Appends_To_Its_Log()
    {
        // Arrange
        var node = new Node() { State = NodeState.Leader };
        Entry expectedEntry = new(12345, "12345", node.Term );

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
        fauxNode.State = NodeState.Follower;
        var node = new Node([fauxNode]);
        fauxNode.Id = node.Id + 1;
        fauxNode.Log = [];


        // Act
        node.BecomeLeader();

        // Assert
        fauxNode.NextIndex.Should().Be(node.Log.Count);
    }

    // Test 5
    [Fact]
    public async Task Leaders_Maintain_NextIndex_ForEach_Follower_That_Matches_The_Next_Log_It_Will_Send()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        fauxNode.Id = 1;
        var fauxNode2 = Substitute.For<INode>();
        fauxNode2.Id = 2;
        var node = new Node([fauxNode, fauxNode2]);

        node.Log = [
                new Entry(1,"Command1", node.Term), // 0 
                new Entry(2, "Command2", node.Term), // 1
                new Entry(3, "Command3", node.Term), // 2
                new Entry(4, "Command4", node.Term)  // INDEX 3, COUNT = 4, NEXT INDEX = 4
            ];

        fauxNode.Log = [
                new Entry(1, "Command1", node.Term), // 0
                new Entry(2, "Command2", node.Term), // 1
                new Entry(3, "Command3", node.Term), // INDEX = 2, COUNT = 3, NEXT INDEX = 3
            ];

        fauxNode2.Log = [
                new Entry(1, "Command1", node.Term), // INDEX = 0, COUNT = 1, NEXT INDEX = 1
            ];

        node.OtherNextIndexes[1] = 3;
        node.OtherNextIndexes[2] = 1;

        // Act
        node.BecomeLeader();
        await Task.Delay(200);

        // Assert
        await fauxNode.Received().ReceiveHeartbeat(
            node.Term,
            node.Id,
            node.CommittedIndex,
            node.Log.Count - 1,
            node.Log[^1].Term,
            Arg.Is<List<Entry>>(entries =>
                entries.Count == 1 &&  
                entries[0].Command == "Command4" &&
                entries[0].Term == node.Term
            )
        );

        await fauxNode2.Received().ReceiveHeartbeat(
            node.Term,
            node.Id,
            node.CommittedIndex,
            node.Log.Count - 1,
            node.Log[^1].Term,
            Arg.Is<List<Entry>>(entries =>
                entries.Count == 3 && 
                entries[0].Command == "Command2" &&
                entries[0].Term == node.Term
                &&
                entries[1].Command == "Command3" &&
                entries[1].Term == node.Term
                &&
                entries[2].Command == "Command4" &&
                entries[2].Term == node.Term
            )
        );
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
        fauxNode.Log = [entry];
        node.Log = [entry];

        // Act
        node.BecomeLeader();

        // Assert
        await fauxNode.Received().ReceiveHeartbeat(
            node.Term,
            node.Id,
            node.CommittedIndex,
            node.Log.Count - 1,
            node.Log[^1].Term 
        );
    }

    // Test 7
    [Fact]
    public async Task When_Follower_Learns_Log_Is_Committed_It_Too_Commits_Log()
    {
        // Arrange
        var fauxLeaderNode = Substitute.For<INode>();
        fauxLeaderNode.State = NodeState.Leader;
        var node = new Node([fauxLeaderNode]);
        fauxLeaderNode.Term = 1;
        fauxLeaderNode.Id = node.Id + 1;
        Entry entry = new(1, "Command1", node.Term);
        fauxLeaderNode.Log = [ entry ];
        fauxLeaderNode.CommittedIndex = 0;
        node.Log = [ entry ];

        // Act
        await node.ReceiveHeartbeat(fauxLeaderNode.Term, fauxLeaderNode.Id, fauxLeaderNode.CommittedIndex, fauxLeaderNode.Log.Count - 1, fauxLeaderNode.Log[^1].Term);

        // Assert
        node.StateMachine.Should().Contain(1, "Command1");
    }

    // Test 8
    [Fact]
    public async Task Leader_Commits_Log_After_Receiving_Majority_Response()
    {
        //Arrange
        var node = new Node();
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];

        // Act
        await node.RespondHeartbeat(node.Id, node.Term, node.Log.Count - 1, true, true);
        await node.RespondHeartbeat(node.Id, node.Term, node.Log.Count - 1, true, true);

        // Assert
        node.StateMachine.Should().Contain(1, "Command1");
    }

    // Test 9
    [Fact]
    public async Task Leader_Commits_Log_By_Incrementing_CommittedLogIndex()
    {
        //Arrange
        var node = new Node();
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];

        // Act
        await node.RespondHeartbeat(node.Id, node.Term, node.Log.Count - 1, true, true);
        await node.RespondHeartbeat(node.Id, node.Term, node.Log.Count - 1, true, true);

        // Assert
        node.CommittedIndex.Should().Be(0);
    }

    // Test 10
    [Fact]
    public async Task When_Follower_Receives_Logs_It_Adds_Them_To_Personal_Log()
    {
        // Arrange
        var fauxLeaderNode = Substitute.For<INode>();
        fauxLeaderNode.State = NodeState.Leader;
        var node = new Node([fauxLeaderNode]);
        fauxLeaderNode.Term = 1;
        fauxLeaderNode.Id = node.Id + 1;
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];
        fauxLeaderNode.Log = [entry];
        fauxLeaderNode.CommittedIndex = 0;

        // Act
        await node.ReceiveHeartbeat(fauxLeaderNode.Term, fauxLeaderNode.Id, fauxLeaderNode.CommittedIndex, fauxLeaderNode.Log.Count - 1, fauxLeaderNode.Log[^1].Term);

        // Assert
        node.Log.Should().Contain(entry);
    }

    // Test 11
    [Fact]
    public async Task Followers_Response_To_AppendEntriesRPC_Includes_Term_And_LogEntryIndex()
    {
        // Arrange
        var fauxLeader = Substitute.For<INode>();
        fauxLeader.State = NodeState.Leader;
        fauxLeader.Term = 1;
        fauxLeader.CommittedIndex = 0;
        var node = new Node([fauxLeader]);
        fauxLeader.Id = node.Id + 1;
        Entry entry = new(1, "Command1", node.Term);
        fauxLeader.Log = [entry];
        node.Log = [entry];

        // Act
        await node.ReceiveHeartbeat(fauxLeader.Term, fauxLeader.Id, fauxLeader.CommittedIndex, fauxLeader.Log.Count - 1, fauxLeader.Log[^1].Term);

        // Assert
        await fauxLeader.Received(1).RespondHeartbeat(
            node.Term,
            node.Log.Count - 1 //IDK IF THIS IS RIGHT
        );
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
        fauxLeaderNode.State = NodeState.Leader;
        fauxLeaderNode.CommittedIndex = 0;
        fauxLeaderNode.Term = 1;
        var node = new Node([fauxLeaderNode]);
        fauxLeaderNode.Id = node.Id + 1;
        Entry entry = new(1, "Command1", fauxLeaderNode.Term);
        fauxLeaderNode.Log = [entry];
        node.Log = [entry];

        // Act
        await node.ReceiveHeartbeat(fauxLeaderNode.Term, fauxLeaderNode.Id, fauxLeaderNode.CommittedIndex, fauxLeaderNode.Log.Count - 1, fauxLeaderNode.Log[^1].Term);

        // Assert
        node.CommittedIndex.Should().Be(fauxLeaderNode.CommittedIndex);
    }

    // Test 15a
    [Fact]
    public async Task When_Sending_AppendEntries_It_Sends_PrevLogIndex_And_PrevLogTerm()
    {
        //Arrange
        var fauxNode = Substitute.For<INode>();
        var node = new Node([fauxNode]);
        fauxNode.Id= node.Id + 1;
        Entry entry = new(1, "Command1", node.Term);
        node.Log = [entry];
        fauxNode.Log = [];

        // Act
        node.BecomeLeader();

        // Assert
        await fauxNode.Received().ReceiveHeartbeat(node.Term, node.Id, node.CommittedIndex, node.Log.Count - 1, node.Log[^1].Term);
    }

    //// Test 15b
    //[Fact]
    //public async Task If_Follower_Log_DoesNot_Have_PreLogIndex_And_PreLogTerm_It_Rejects()
    //{
    //    //Arrange
    //    var fauxLeaderNode = Substitute.For<INode>();
    //    fauxLeaderNode.State = NodeState.Leader;
    //    var node = new Node([fauxLeaderNode]);
    //    node.OtherNextIndexes.Add(fauxLeaderNode.Id, node.Log.Count);
    //    fauxLeaderNode.Id = node.Id + 1;
    //    Entry entry = new(1, "Command1", node.Term);
    //    Entry entry2 = new(2, "Command2", node.Term);
    //    Entry entry3 = new(3, "Command3", node.Term);
    //    fauxLeaderNode.Log = [entry, entry2, entry3];
    //    node.Log = [entry];

    //    // Act
    //    await node.SendHeartbeat();

    //    // Assert
    //    await fauxLeaderNode.Received().ReceiveHeartbeat(node.Term, node.Id, node.CommittedIndex, node.Log.Count - 1, node.Log[^1].Term);
    //}

}