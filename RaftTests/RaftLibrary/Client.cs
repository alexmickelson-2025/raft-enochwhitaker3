

namespace RaftLibrary;

public class Client : IClient
{
    public string Command { get; set; }
    public int LeaderId { get; set;}
    public List<string> Responses { get; set; }

    public Client()
    {
        Command = "";
        Responses = new List<string>();
    }

    public async Task appendResponses(string newResponse)
    {
        Responses.Add(newResponse);
        await Task.CompletedTask;
    }
    public async Task hasCommittedCommand(int key, string entry)
    {
        await appendResponses($"Node {LeaderId} has Committed: `{entry}`");
    }
}
