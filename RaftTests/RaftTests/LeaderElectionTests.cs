using Castle.Core.Logging;
using FluentAssertions;
using NSubstitute;
using RaftLibrary;

namespace RaftTests;
public class LeaderElectionTests
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
        await node.ReceiveHeartbeat(node.Term, leader.Id, null, 0, 1);

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
        startInterval.Should().NotBe(middleInterval);
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
        follower1.Log = [];

        // Act
        await leader.SendVote();
        await leader.SendVote();
        leader.CheckElection();
        await Task.Delay(200);

        // Assert
        await follower1.Received(5).ReceiveHeartbeat(leader.Term, leader.Id, leader.CommittedIndex, 0,1);
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
        await follower.ReceiveHeartbeat(follower.Term - 1, leader.Id, leader.CommittedIndex,0,0);

        // Assert
        await leader.Received(0).RespondHeartbeat(follower.Term, follower.Log.Count - 1);
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
        await follower.ReceiveHeartbeat(leader.Term, leader.Id, leader.CommittedIndex,0,0);

        // Assert
        await leader.Received(1).RespondHeartbeat(follower.Term, follower.Log.Count - 1);
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
        node.CheckElection();

        // Assert
        node.State.Should().Be(NodeState.Leader);
    }

    //Test #2
    [Fact]
    public async Task If_A_Follower_Gets_RPC_From_Another_Node_It_Remembers_The_Sender_Is_The_Leader()
    {
        // Arrange
        var fauxLeader = Substitute.For<INode>();
        fauxLeader.State = NodeState.Leader;
        var node = new Node([fauxLeader]);
        fauxLeader.Id = node.Id + 1;
        fauxLeader.Term = node.Term;

        // Act
        await node.ReceiveHeartbeat(fauxLeader.Term, fauxLeader.Id,null,0,0);

        // Assert
        node.LeaderId.Should().Be(fauxLeader.Id);
    }

    //Test #19
    [Fact]
    public async Task When_A_Candidate_Becomes_Leader_It_Immediately_Sends_Heartbeat()
    {
        // Arrange
        var fauxNode1 = Substitute.For<INode>();
        var fauxNode2 = Substitute.For<INode>();
        var node = new Node([fauxNode1, fauxNode2]) { State = NodeState.Follower };
        fauxNode1.Log = [];
        fauxNode2.Log = [];

        // Act
        node.BecomeCandidate();
        await node.SendVote();
        node.CheckElection();

        // Assert
        await fauxNode1.Received().ReceiveHeartbeat(node.Term, node.Id, node.CommittedIndex, 0, 1);
        await fauxNode2.Received().ReceiveHeartbeat(node.Term, node.Id, node.CommittedIndex, 0 ,1);
        node.State.Should().Be(NodeState.Leader);
    }

    //Test #9
    [Fact]
    public async Task If_A_Candidate_Gets_Majority_Votes_With_Unresponsive_Node_It_Still_Becomes_Leader()
    {
        // Arrange
        var fauxNode1 = Substitute.For<INode>();
        var fauxNode2 = Substitute.For<INode>();
        var node = new Node([fauxNode1, fauxNode2]) { State = NodeState.Follower };
        fauxNode1.Log = [];
        fauxNode2.Log = [];

        // Act
        node.BecomeCandidate();
        await node.SendVote();
        node.CheckElection();

        // Assert
        await fauxNode1.Received().ReceiveHeartbeat(node.Term, node.Id, node.CommittedIndex, 0, 1);
        await fauxNode2.Received().ReceiveHeartbeat(node.Term, node.Id, node.CommittedIndex, 0, 1);
        node.State.Should().Be(NodeState.Leader);
    }

    //Test #12
    [Fact]
    public async Task If_A_Candidate_Receives_RPC_From_Later_Term_It_Returns_To_Follower()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        var fauxLeaderNode = Substitute.For<INode>();
        fauxLeaderNode.State = NodeState.Leader;
        var candidateNode = new Node([fauxNode, fauxLeaderNode]) { State = NodeState.Follower };
        fauxLeaderNode.Term = candidateNode.Term + 2;
        fauxLeaderNode.Id = candidateNode.Id + 1;

        // Act
        candidateNode.BecomeCandidate();
        candidateNode.Term.Should().BeLessThan(fauxLeaderNode.Term);
        candidateNode.State.Should().Be(NodeState.Candidate);
        await candidateNode.ReceiveHeartbeat(fauxLeaderNode.Term, fauxLeaderNode.Id, null, 0, 0);

        // Assert
        candidateNode.State.Should().Be(NodeState.Follower);
        await fauxLeaderNode.Received(1).RespondHeartbeat(candidateNode.Term, candidateNode.Log.Count - 1);
    }

    //Test #13
    [Fact]
    public async Task If_A_Candidate_Receives_RPC_From_Equal_Term_It_Returns_To_Follower()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        var fauxLeaderNode = Substitute.For<INode>();
        fauxLeaderNode.State = NodeState.Leader;
        var candidateNode = new Node([fauxNode, fauxLeaderNode]) { State = NodeState.Follower };
        fauxLeaderNode.Term = candidateNode.Term + 1;
        fauxLeaderNode.Id = candidateNode.Id + 1;

        // Act
        candidateNode.BecomeCandidate();
        candidateNode.Term.Should().Be(fauxLeaderNode.Term);
        candidateNode.State.Should().Be(NodeState.Candidate);
        await candidateNode.ReceiveHeartbeat(fauxLeaderNode.Term, fauxLeaderNode.Id, null, 0, 0);

        // Assert
        candidateNode.State.Should().Be(NodeState.Follower);
        await fauxLeaderNode.Received(1).RespondHeartbeat(candidateNode.Term, candidateNode.Log.Count - 1);
    }

    //Test #15
    [Fact]
    public async Task If_A_Follower_Receives_A_Second_VoteRequest_From_A_Future_Term_It_Votes_Yes()
    {
        // Arrange
        var fauxCandidate = Substitute.For<INode>();
        fauxCandidate.State = NodeState.Candidate;
        var fauxCandidate2 = Substitute.For<INode>();
        fauxCandidate2.State = NodeState.Candidate;
        var followerNode = new Node([fauxCandidate, fauxCandidate2]);
        fauxCandidate.Id = followerNode.Id + 1;
        fauxCandidate.Term = followerNode.Term + 1;
        fauxCandidate2.Id = followerNode.Id + 2;
        fauxCandidate2.Term = followerNode.Term + 2;

        // Act
        await followerNode.ReceiveRequestVote(fauxCandidate.Id);
        await followerNode.ReceiveRequestVote(fauxCandidate2.Id);

        // Assert
        await fauxCandidate.Received(1).SendVote();
        await fauxCandidate2.Received(1).SendVote();
    }

    //Test #14
    [Fact]
    public async Task If_A_Follower_Receives_A_Second_VoteRequest_From_The_Same_Term_It_Votes_No()
    {
        // Arrange
        var fauxCandidate = Substitute.For<INode>();
        fauxCandidate.State = NodeState.Candidate;
        var fauxCandidate2 = Substitute.For<INode>();
        fauxCandidate2.State = NodeState.Candidate;
        var followerNode = new Node([fauxCandidate, fauxCandidate2]);
        fauxCandidate.Id = followerNode.Id + 1;
        fauxCandidate.Term = followerNode.Term + 1;
        fauxCandidate2.Id = fauxCandidate.Id + 1;
        fauxCandidate2.Term = fauxCandidate.Term;

        // Act
        await followerNode.ReceiveRequestVote(fauxCandidate.Id);
        await followerNode.ReceiveRequestVote(fauxCandidate2.Id);

        // Assert
        await fauxCandidate.Received(1).SendVote();
        await fauxCandidate2.Received(0).SendVote();
    }
}