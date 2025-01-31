using static RaftLibrary.DTOs;

namespace RaftLibrary;
public interface INode
{
    public int Id { get; set; }
    Task ReceiveHeartbeat(ReceiveHeartbeatDTO Data);
    Task RespondHeartbeat(RespondHeartbeatDTO Data);
    Task SendVote();
    Task RequestVotes(RequestVoteDTO Data);
    Task ReceiveRequestVote(ReceiveRequestVoteDTO Data);
}
