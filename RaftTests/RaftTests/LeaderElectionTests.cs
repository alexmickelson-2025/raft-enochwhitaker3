using Castle.Core.Logging;
using FluentAssertions;
using NSubstitute;
using RaftLibrary;
using static RaftLibrary.DTOs;

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
        Node node = new Node([leader]);
        leader.Id = node.Id + 1;
        var startTime = node.Timer.Interval;

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = leader.Id,
            prevLogIndex = null,
            prevLogTerm = null,
            leadersCommitIndex = null
        };
        await node.ReceiveHeartbeat(heartbeatData);

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

        // Act
        await leader.SendVote();
        await leader.SendVote();
        leader.CheckElection();
        await Task.Delay(200);

        // Assert
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = leader.Term,
            receivedLeaderId = leader.Id,
            prevLogIndex = null,
            prevLogTerm = null,
            leadersCommitIndex = null
        };
        await follower1.Received(5).ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
            data.receivedTermId == heartbeatData.receivedTermId
            && data.receivedLeaderId == heartbeatData.receivedLeaderId
            && data.prevLogIndex == heartbeatData.prevLogIndex
            && data.prevLogTerm == heartbeatData.prevLogTerm
            && data.leadersCommitIndex == heartbeatData.leadersCommitIndex
        ));
    }

    //Test #18
    [Fact]
    public async Task Candidate_Rejects_RPC_If_Term_Is_Older()
    {
        // Arrange
        var fauxLeaderNode = Substitute.For<INode>();
        var node = new Node([fauxLeaderNode]);
        fauxLeaderNode.Id = node.Id + 1;

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term - 1,
            receivedLeaderId = fauxLeaderNode.Id,
            prevLogIndex = null,
            prevLogTerm = null,
            leadersCommitIndex = null
        };
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        var responseData = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = node.Log.Count - 1,
            acceptedRPC = true,
        };;
        await fauxLeaderNode.Received(0).RespondHeartbeat(Arg.Is<RespondHeartbeatDTO>(data =>
            data.id == responseData.id
            && data.term == responseData.term
            && data.logIndex == responseData.logIndex
            && data.acceptedRPC == responseData.acceptedRPC
        ));
    }

    //Test #17
    [Fact]
    public async Task Candidate_Responds_To_RPC()
    {
        // Arrange
        var fauxLeaderNode = Substitute.For<INode>();
        var node = new Node([fauxLeaderNode]);
        fauxLeaderNode.Id = 1234;

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = fauxLeaderNode.Id,
            prevLogIndex = null,
            prevLogTerm = null,
            leadersCommitIndex = null
        };
        await node.ReceiveHeartbeat(heartbeatData);

        // Assert
        var responseData = new RespondHeartbeatDTO
        {
            id = node.Id,
            term = node.Term,
            logIndex = null,
            acceptedRPC = true,
        }; 
        await fauxLeaderNode.Received(1).RespondHeartbeat(Arg.Is<RespondHeartbeatDTO>(data =>
            data.id == responseData.id
            && data.term == responseData.term
            && data.logIndex == responseData.logIndex
            && data.acceptedRPC == responseData.acceptedRPC
        ));
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
        fauxCandidate.Id = followerNode.Id + 1;

        //Act
        var requestData = new ReceiveRequestVoteDTO
        {
            candidateId = fauxCandidate.Id,
            candidateTerm = followerNode.Term + 1,
        };
        await followerNode.ReceiveRequestVote(requestData);

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
        var node = new Node([fauxLeader]);
        fauxLeader.Id = node.Id + 1;

        // Act
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = fauxLeader.Id,
            prevLogIndex = null,
            prevLogTerm = null,
            leadersCommitIndex = null
        };
        await node.ReceiveHeartbeat(heartbeatData);

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

        // Act
        node.BecomeCandidate();
        await node.SendVote();
        node.CheckElection();

        // Assert
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = node.Id,
            prevLogIndex = null,
            prevLogTerm = null,
            leadersCommitIndex = null
        };

        await fauxNode1.Received().ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
            data.receivedTermId == heartbeatData.receivedTermId
            && data.receivedLeaderId == heartbeatData.receivedLeaderId
            && data.prevLogIndex == heartbeatData.prevLogIndex
            && data.prevLogTerm == heartbeatData.prevLogTerm
            && data.leadersCommitIndex == heartbeatData.leadersCommitIndex
        ));
        await fauxNode2.Received().ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
            data.receivedTermId == heartbeatData.receivedTermId
            && data.receivedLeaderId == heartbeatData.receivedLeaderId
            && data.prevLogIndex == heartbeatData.prevLogIndex
            && data.prevLogTerm == heartbeatData.prevLogTerm
            && data.leadersCommitIndex == heartbeatData.leadersCommitIndex
        ));

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

        // Act
        node.BecomeCandidate();
        await node.SendVote();
        node.CheckElection();

        // Assert
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = node.Id,
            prevLogIndex = null,
            prevLogTerm = null,
            leadersCommitIndex = null
        };

        await fauxNode1.Received().ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
            data.receivedTermId == heartbeatData.receivedTermId
            && data.receivedLeaderId == heartbeatData.receivedLeaderId
            && data.prevLogIndex == heartbeatData.prevLogIndex
            && data.prevLogTerm == heartbeatData.prevLogTerm
            && data.leadersCommitIndex == heartbeatData.leadersCommitIndex
        ));
        await fauxNode2.Received().ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
            data.receivedTermId == heartbeatData.receivedTermId
            && data.receivedLeaderId == heartbeatData.receivedLeaderId
            && data.prevLogIndex == heartbeatData.prevLogIndex
            && data.prevLogTerm == heartbeatData.prevLogTerm
            && data.leadersCommitIndex == heartbeatData.leadersCommitIndex
        ));

        node.State.Should().Be(NodeState.Leader);
    }

    //Test #12
    [Fact]
    public async Task If_A_Candidate_Receives_RPC_From_Later_Term_It_Returns_To_Follower()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        var fauxLeaderNode = Substitute.For<INode>();
        var candidateNode = new Node([fauxNode, fauxLeaderNode]) { State = NodeState.Follower };
        fauxLeaderNode.Id = candidateNode.Id + 1;

        // Act
        candidateNode.BecomeCandidate();
        candidateNode.Term.Should().BeLessThan(candidateNode.Term + 2);
        candidateNode.State.Should().Be(NodeState.Candidate);
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = candidateNode.Term + 2,
            receivedLeaderId = fauxLeaderNode.Id,
            prevLogIndex = null,
            prevLogTerm = null,
            leadersCommitIndex = null
        };
        await candidateNode.ReceiveHeartbeat(heartbeatData);

        // Assert
        candidateNode.State.Should().Be(NodeState.Follower);
        var responseData = new RespondHeartbeatDTO
        {
            id = candidateNode.Id,
            term = candidateNode.Term,
            logIndex = null,
            acceptedRPC = true,
        };
        await fauxLeaderNode.Received(1).RespondHeartbeat(Arg.Is<RespondHeartbeatDTO>(data =>
            data.id == responseData.id
            && data.term == responseData.term
            && data.logIndex == responseData.logIndex
            && data.acceptedRPC == responseData.acceptedRPC
        ));
    }

    //Test #13
    [Fact]
    public async Task If_A_Candidate_Receives_RPC_From_Equal_Term_It_Returns_To_Follower()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        var fauxLeaderNode = Substitute.For<INode>();
        var candidateNode = new Node([fauxNode, fauxLeaderNode]) { State = NodeState.Follower };
        int leaderTerm = candidateNode.Term + 1;
        fauxLeaderNode.Id = candidateNode.Id + 1;

        // Act
        candidateNode.BecomeCandidate();
        candidateNode.Term.Should().Be(leaderTerm);
        candidateNode.State.Should().Be(NodeState.Candidate);
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = leaderTerm,
            receivedLeaderId = fauxLeaderNode.Id,
            prevLogIndex = null,
            prevLogTerm = null,
            leadersCommitIndex = null
        };
        await candidateNode.ReceiveHeartbeat(heartbeatData);

        // Assert
        candidateNode.State.Should().Be(NodeState.Follower);
        var responseData = new RespondHeartbeatDTO
        {
            id = candidateNode.Id,
            term = candidateNode.Term,
            logIndex = null,
            acceptedRPC = true,
        };
        await fauxLeaderNode.Received(1).RespondHeartbeat(Arg.Is<RespondHeartbeatDTO>(data =>
            data.id == responseData.id
            && data.term == responseData.term
            && data.logIndex == responseData.logIndex
            && data.acceptedRPC == responseData.acceptedRPC
        ));
    }

    //Test #15
    [Fact]
    public async Task If_A_Follower_Receives_A_Second_VoteRequest_From_A_Future_Term_It_Votes_Yes()
    {
        // Arrange
        var fauxCandidate = Substitute.For<INode>();
        var fauxCandidate2 = Substitute.For<INode>();
        var followerNode = new Node([fauxCandidate, fauxCandidate2]);
        fauxCandidate.Id = followerNode.Id + 1;
        int candidateTerm1 = followerNode.Term + 1;
        fauxCandidate2.Id = followerNode.Id + 2;
        int candidateTerm2 = followerNode.Term + 2;

        // Act
        var requestData1 = new ReceiveRequestVoteDTO
        {
            candidateId = fauxCandidate.Id,
            candidateTerm = candidateTerm1,
        };
        var requestData2 = new ReceiveRequestVoteDTO
        {
            candidateId = fauxCandidate2.Id,
            candidateTerm = candidateTerm2,
        };
        await followerNode.ReceiveRequestVote(requestData1);
        await followerNode.ReceiveRequestVote(requestData2);

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
        var fauxCandidate2 = Substitute.For<INode>();
        var followerNode = new Node([fauxCandidate, fauxCandidate2]);
        int term = followerNode.Term + 1;
        fauxCandidate.Id = followerNode.Id + 1;
        fauxCandidate2.Id = fauxCandidate.Id + 1;

        // Act
        var requestData1 = new ReceiveRequestVoteDTO
        {
            candidateId = fauxCandidate.Id,
            candidateTerm = term,
        };
        var requestData2 = new ReceiveRequestVoteDTO
        {
            candidateId = fauxCandidate2.Id,
            candidateTerm = term,
        };
        await followerNode.ReceiveRequestVote(requestData1);
        await followerNode.ReceiveRequestVote(requestData2);

        // Assert
        await fauxCandidate.Received(1).SendVote();
        await fauxCandidate2.Received(0).SendVote();
    }

    //Test in class #1
    [Fact]
    public async Task When_Leader_Node_Is_In_Election_Loop_Then_They_Get_Paused_Other_Nodes_DoNot_Get_Heartbeat_For_400ms()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        var node = new Node([fauxNode]);
        fauxNode.Id = node.Id + 1;

        // Act
        node.BecomeLeader();
        node.TogglePause(true);
        await Task.Delay(400);

        // Assert
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = node.Id,
            prevLogIndex = null,
            prevLogTerm = null,
            leadersCommitIndex = null
        };
        await fauxNode.Received().ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
            data.receivedTermId == heartbeatData.receivedTermId
            && data.receivedLeaderId == heartbeatData.receivedLeaderId
            && data.prevLogIndex == heartbeatData.prevLogIndex
            && data.prevLogTerm == heartbeatData.prevLogTerm
            && data.leadersCommitIndex == heartbeatData.leadersCommitIndex
        ));
    }

    //Test in class #2
    [Fact]
    public async Task When_Leader_Node_Is_In_Election_Loop_Then_They_Get_Paused_Other_Nodes_DoNot_Get_Heartbeat_For_400ms_Then_When_Unpaused_They_Recieve_Again()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        var node = new Node([fauxNode]);
        fauxNode.Id = node.Id + 1;

        // Act
        node.BecomeLeader();
        node.TogglePause(true);
        await Task.Delay(400);
        node.TogglePause(false);
        await Task.Delay(100);

        // Assert
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = node.Term,
            receivedLeaderId = node.Id,
            prevLogIndex = null,
            prevLogTerm = null,
            leadersCommitIndex = null
        };
        await fauxNode.Received(2).ReceiveHeartbeat(Arg.Is<ReceiveHeartbeatDTO>(data =>
            data.receivedTermId == heartbeatData.receivedTermId
            && data.receivedLeaderId == heartbeatData.receivedLeaderId
            && data.prevLogIndex == heartbeatData.prevLogIndex
            && data.prevLogTerm == heartbeatData.prevLogTerm
            && data.leadersCommitIndex == heartbeatData.leadersCommitIndex
        ));
    }

    //Test in class #3
    [Fact]
    public async Task When_Follower_Gets_Paused_It_Does_Not_Become_A_Candidate()
    {
        // Arrange
        var node = new Node();

        // Act
        node.TogglePause(true);
        await Task.Delay(400);

        // Assert
        node.State.Should().Be(NodeState.Follower);
    }

    //Test in class #4
    [Fact]
    public async Task When_Follower_Gets_UnPaused_After_Being_Paused_It_Eventually_Becomes_Candidate()
    {
        // Arrange
        var node = new Node();

        // Act
        node.TogglePause(true);
        await Task.Delay(400);
        node.TogglePause(false);
        await Task.Delay(300);

        // Assert
        node.State.Should().Be(NodeState.Candidate);
    }
}