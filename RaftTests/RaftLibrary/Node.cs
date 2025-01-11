namespace RaftLibrary;

public class Node
{
    public string State { get; private set; } = "Follower";
    public void ReceiveHeartbeat()
    {
        State = "Follower";
    }
}

