
namespace RaftLibrary;

public class Client : IClient
{
    public string Command { get; set; }

    public Client()
    {
        Command = "";
    }

    public async Task hasCommittedCommand(int Key)
    {
        await Task.CompletedTask;
    }
}
