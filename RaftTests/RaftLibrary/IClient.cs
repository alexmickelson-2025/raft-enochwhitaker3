namespace RaftLibrary;
public interface IClient
{
    public string Command { get; set; }
    public int LeaderId { get; set; }
    public List<string> Responses { get; set; }
    Task hasCommittedCommand(int Key, string Entry);
    Task appendResponses(string newResponse);
}
