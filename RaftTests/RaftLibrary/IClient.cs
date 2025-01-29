namespace RaftLibrary;
public interface IClient
{
    public string Command { get; set; }
    Task hasCommittedCommand(int Key);
}
