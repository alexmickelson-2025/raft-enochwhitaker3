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
    public async void FollowersTimer_Should_Reset_After_Received_Message()
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
        node.Votes.Should().Contain(node.Id, node.Term);
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
        var leader = new Node(new List<INode> { follower1 });

        // Act
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
        await follower.ReceiveHeartbeat(leader.Term - 1, leader.Id);

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
}