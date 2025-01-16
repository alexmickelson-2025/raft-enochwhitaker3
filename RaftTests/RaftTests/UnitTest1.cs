using Castle.Core.Logging;
using FluentAssertions;
using NSubstitute;
using RaftLibrary;

namespace RaftTests;
public class UnitTest1
{
    //Test #3
    [Fact]
    public void NewNode_Should_BeInFollowerState()
    {
        // Arrange
        Node node = new Node();

        // Act
        var state = node.State;

        // Assert
        state.Should().Be(NodeState.Follower);
    }

    //Test #7
    [Fact]
    public async Task FollowersTimer_Should_Reset_After_Received_Message()
    {
        // Arrange
        var leader = Substitute.For<INode>();
        leader.State = NodeState.Leader;
        leader.Id = 1234;
        Node node = new Node([leader]);
        leader.Term = node.Term;
        var startTime = node.Timer.Interval;

        // Act
        Thread.Sleep(200);
        await node.ReceiveHeartbeat(node.Term, leader.Id);

        // Assert
        node.Timer.Interval.Should().NotBe(startTime);
        node.State.Should().Be(NodeState.Follower);
    }

    //Test #4
    [Fact]
    public void Follower_Should_StartElection_When_NoMessageReceivedFor300ms()
    {
        // Arrange
        var node = new Node();

        // Act
        Thread.Sleep(350);

        // Assert
        node.State.Should().Be(NodeState.Candidate);
    }

    //Test #5
    [Fact]
    public void ElectionTime_Should_Be_Random_After_Timeout()
    {
        // Arrange
        var node = new Node();
        double startInterval = node.Timer.Interval;

        //Act
        Thread.Sleep(350);
        double middleInterval = node.Timer.Interval;
        Thread.Sleep(350);

        //Assert
        node.Timer.Interval.Should().NotBe(startInterval);
        node.Timer.Interval.Should().NotBe(middleInterval);
    }

    //Test #11
    [Fact]
    public void Candidate_Votes_ForItself_After_Becoming_Candidate()
    {
        // Arrange
        var node = new Node();

        // Act
        Thread.Sleep(350);

        // Assert
        node.State.Should().Be(NodeState.Candidate);
        node.Votes.Should().Contain(node.Term);
    }

    //Test #6
    [Fact]
    public void When_A_New_Election_Begins_Increment_Term_By_1()
    {
        // Arrange
        var node = new Node();
        var initialTerm = node.Term;

        // Act
        Thread.Sleep(350);

        // Assert
        node.Term.Should().BeGreaterThan(initialTerm);
    }

    //Test #1
    [Fact]
    public async Task Leader_SendsHeartbeat_Every50ms()
    {
        // Arrange
        var follower1 = Substitute.For<INode>();
        var leader = new Node([follower1]);

        // Act
        await leader.SendVote();
        await leader.SendVote();
        leader.BecomeLeader();
        await Task.Delay(200);

        // Assert
        await follower1.Received(4).ReceiveHeartbeat(leader.Term);
    }

    //Test #18
    [Fact]
    public async Task Candidate_Rejects_RPC_If_Term_Is_Older()
    {
        // Arrange
        var leader = Substitute.For<INode>();
        leader.State = NodeState.Leader;
        var follower = new Node([leader]);
        leader.Term = follower.Term;
        leader.Id = 1234;

        // Act
        await follower.ReceiveHeartbeat(follower.Term - 1, leader.Id);

        // Assert
        await leader.Received(0).RespondHeartbeat();
    }

    //Test #17
    [Fact]
    public async Task Candidate_Responds_To_RPC()
    {
        // Arrange
        var leader = Substitute.For<INode>();
        leader.State = NodeState.Leader;
        var follower = new Node([leader]);
        leader.Term = follower.Term;
        leader.Id = 1234;

        // Act
        await follower.ReceiveHeartbeat(leader.Term, leader.Id);

        // Assert
        await leader.Received(1).RespondHeartbeat();
    }

    //Test #16
    [Fact]
    public void When_The_Election_Timer_Expires_In_An_Election_A_New_One_Begins()
    {
        // Arrange
        var node = new Node();

        // Act
        Thread.Sleep(350);
        var firstTimer = node.Timer.Interval;
        Thread.Sleep(350);
        var secondTimer = node.Timer.Interval;

        // Assert
        node.State.Should().Be(NodeState.Candidate);
        firstTimer.Should().NotBe(secondTimer);
    }


    //Test #10
    [Fact]
    public async Task If_A_Node_HasNot_Voted_And_Gets_RequestVoteRPC_It_Responds_Yes()
    {
        //Arrange
        var fauxCandidate = Substitute.For<INode>();
        var followerNode = new Node([fauxCandidate]);
        fauxCandidate.Term = followerNode.Term + 1;
        fauxCandidate.Id = followerNode.Id + 1;

        //Act
        await followerNode.ReceiveRequestVote(fauxCandidate.Id);

        //Assert
        await fauxCandidate.Received(1).SendVote();
    }

    //Test #8
    [Fact]
    public async Task If_A_Candidate_Gets_Majority_Votes_Then_It_Becomes_A_Leader()
    {
        // Arrange
        var fauxNode1 = Substitute.For<INode>();
        var fauxNode2 = Substitute.For<INode>();
        var node = new Node([fauxNode1, fauxNode2]) { State = NodeState.Follower };

        // Act
        node.BecomeCandidate();
        await node.SendVote();
        node.BecomeLeader();

        // Assert
        node.State.Should().Be(NodeState.Leader);
    }
}