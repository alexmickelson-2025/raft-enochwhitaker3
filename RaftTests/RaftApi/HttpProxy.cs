using RaftLibrary;

public class HttpRpcOtherNode : INode
{
    public int Id { get; set; }
    public string Url { get; }

    private HttpClient client = new();

    public HttpRpcOtherNode(int id, string url)
    {
        Id = id;
        Url = url;
    }

    public async Task RequestVotes(DTOs.RequestVoteDTO Data)
    {
        try
        {
            await client.PostAsJsonAsync(Url + "/request/votes", Data);
        }
        catch (HttpRequestException)
        {
            Console.WriteLine($"node {Url} is down");
        }
    }

    public async Task ReceiveHeartbeat(DTOs.ReceiveHeartbeatDTO Data)
    {
        try
        {
            await client.PostAsJsonAsync(Url + "/receive/heartbeat", Data);
        }
        catch (HttpRequestException)
        {
            Console.WriteLine($"node {Url} is down");
        }
    }

    public async Task RespondHeartbeat(DTOs.RespondHeartbeatDTO Data)
    {
        try
        {
            await client.PostAsJsonAsync(Url + "/respond/heartbeat", Data);
        }
        catch (HttpRequestException)
        {
            Console.WriteLine($"node {Url} is down");
        }
    }

    public async Task ReceiveRequestVote(DTOs.ReceiveRequestVoteDTO Data)
    {
        try
        {
            await client.PostAsJsonAsync(Url + "/receive/requestvote", Data);
        }
        catch (HttpRequestException)
        {
            Console.WriteLine($"node {Url} is down");
        }
    }

    public async Task SendVote()
    {
        try
        {
            await client.PostAsJsonAsync(Url + "/receive/vote", "");
        }
        catch (HttpRequestException)
        {
            Console.WriteLine($"node {Url} is down");
        }
    }
}