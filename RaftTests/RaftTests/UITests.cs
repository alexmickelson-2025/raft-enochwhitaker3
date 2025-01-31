using FluentAssertions;
using NSubstitute;
using RaftLibrary;
using static RaftLibrary.DTOs;

namespace RaftTests;
public class UiTests
{
    [Fact]
    public async Task When_Receiving_Second_ClientCommand_It_Sends_It_Out()
    {
        // Arrange
        var follower1 = Substitute.For<INode>();
        var node = new Node([follower1]) { State = NodeState.Leader };
        follower1.Id = node.Id + 1;
        Entry entry = new(12345, "12345", node.Term);
        node.Log = [entry, entry, entry];
        var response = new RespondHeartbeatDTO 
        { 
            id = follower1.Id,
            term = node.Term,
            logIndex = 0,
            acceptedRPC = false,
            addedToLog = null
        };
        node.OtherNextIndexes.Add(follower1.Id, 1);

        // Act
        await node.RespondHeartbeat(response);

        // Assert
        node.Log.Should().BeEquivalentTo([entry]);
    }

    [Fact]
    public async Task OppositeLol()
    {
        // Arrange
        var fauxLeader = Substitute.For<INode>();
        var node = new Node([fauxLeader]);
        fauxLeader.Id = node.Id + 1;
        Entry entry = new(12345, "12345", node.Term);
        Entry entry2 = new(1, "1", node.Term);
        node.Log = [entry];
        //the node prevLogIndex = 0
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = fauxLeader.Id,
            prevLogIndex = 2,
            prevLogTerm = node.Term,
            leadersCommitIndex = 0,
        };

        // Act
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        node.Log.Should().BeEquivalentTo([entry]);
    }

}