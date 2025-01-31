using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaftLibrary;

public class DTOs
{
    public record ReceiveHeartbeatDTO
    {
        public int receivedTermId { get; init; }
        public int receivedLeaderId { get; init; }
        public int? prevLogIndex { get; init;}
        public int? prevLogTerm { get; init;}
        public int? leadersCommitIndex { get; init; }
        public List<Entry>? newEntries { get; init; }
    };

    public record RespondHeartbeatDTO
    {
        public int id { get; init; }
        public int term { get; init; }
        public int? logIndex { get; init; }
        public bool acceptedRPC { get; init; }
        public bool? addedToLog { get; init; }
    };

    public record RequestVoteDTO
    {
        public int Id { get; init; }
        public int Term { get; init; }
    };

    public record ReceiveRequestVoteDTO
    {
        public int candidateId { get; init; }
        public int candidateTerm { get; init; }
    };

    public record ClientCommandData
    {
        public int requestedKey { get; init; }
        public string requestedCommand { get; init; } = "";
    };
}
