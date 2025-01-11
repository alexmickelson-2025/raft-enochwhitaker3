using FluentAssertions;
using RaftLibrary;

namespace RaftTests;

public class UnitTest1
{
    [Fact]
    public void Follower_RemainsFollower_WhenHeartbeatReceived()
    {
        // Arrange it!
        var node = new Node();

        // Act on that thang!
        node.ReceiveHeartbeat();

        // Assert it!
        node.State.Should().Be("Follower");
    }
}