namespace RaftLibrary;

public record Entry(int givenkey, string givencommand, int giventerm)
{
    public int Key { get; set; } = givenkey;
    public string Command { get; set; } = givencommand;
    public int Term { get; set; } = giventerm;
}
