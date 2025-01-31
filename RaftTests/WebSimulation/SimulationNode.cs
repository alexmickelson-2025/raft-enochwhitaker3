using RaftLibrary;
using static RaftLibrary.DTOs;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WebSimulation;

public class SimulationNode : INode
{
    public readonly Node InnerNode;
    public int NetworkDelay { get; set; }
    public SimulationNode(Node node, int? networkDelay = null)
    {
        this.InnerNode = node;
        NetworkDelay = networkDelay ?? 0;
    }

    public int Id { get => InnerNode.Id; set => InnerNode.Id = value; }

    public async Task ReceiveHeartbeat(ReceiveHeartbeatDTO Data)
    {
        var heartbeatData = new ReceiveHeartbeatDTO
        {
            receivedTermId = Data.receivedTermId,
            receivedLeaderId = Data.receivedLeaderId,
            prevLogIndex = Data.prevLogIndex,
            prevLogTerm = Data.prevLogTerm,
            leadersCommitIndex = Data.leadersCommitIndex,
            newEntries = Data.newEntries
        };
        await Task.Delay(NetworkDelay).ContinueWith(async (_previousTask) =>
        {
            await InnerNode.ReceiveHeartbeat(heartbeatData);
        });
    }

    public async Task RespondHeartbeat(RespondHeartbeatDTO Data)
    {
        var responseData = new RespondHeartbeatDTO
        {
            id = Data.id,
            term = Data.term,
            logIndex = Data.logIndex,
            acceptedRPC = Data.acceptedRPC,
            addedToLog = Data.addedToLog,
        };
        await Task.Delay(NetworkDelay).ContinueWith(async (_previousTask) =>
        {
            await InnerNode.RespondHeartbeat(responseData);
        });
    }

    public async Task SendVote()
    {
        await Task.Delay(NetworkDelay).ContinueWith(async (_previousTask) =>
        {
            await InnerNode.SendVote();
        });
    }

    public async Task ReceiveRequestVote(ReceiveRequestVoteDTO Data)
    {
        var responseData = new ReceiveRequestVoteDTO
        {
            candidateId = Data.candidateId,
            candidateTerm = Data.candidateTerm,
        };
        await Task.Delay(NetworkDelay).ContinueWith(async (_previousTask) =>
        {
            await InnerNode.ReceiveRequestVote(responseData);
        });
    }
    public async Task SendClientCommand(ClientCommandData data)
    {
        if (!InnerNode.IsRunning)
            return;
        await InnerNode.ReceiveClientCommand(data);
    }

    public Task RequestVotes(RequestVoteDTO Data)
    {
        throw new NotImplementedException();
    }
}
