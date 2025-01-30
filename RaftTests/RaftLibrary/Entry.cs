namespace RaftLibrary;

public record Entry(int key, string command, int term)
{
    public int Key { get; set; } = key;
    public string Command { get; set; } = command;
    //THIS STORES THE LEADERS TERM WHEN IT GETS RECEIVED
    public int Term { get; set; } = term;
}
