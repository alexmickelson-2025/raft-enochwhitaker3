using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaftLibrary;

public record NodeData
{
    public int Id { get; set; }
    public string? Url { get; set; }
    public required System.Timers.Timer Timer { get; set; }
    public bool IsRunning { get; set; }
    public int Term { get; set; }
    public int LeaderId { get; set; }
    public int? CommittedIndex { get; set; }
    public required List<Entry> Log { get; set; }
    public NodeState State { get; set; }
    public required Dictionary<int, string> StateMachine { get; set; }
    public int MinInterval { get; set; }
    public int MaxInterval { get; set; }
    public DateTime StartTime { get; set; }
    public double ElapsedTime { get; set; }
}
