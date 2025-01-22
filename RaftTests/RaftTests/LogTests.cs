using Castle.Core.Logging;
using FluentAssertions;
using NSubstitute;
using RaftLibrary;

namespace RaftTests;
public class LogTests
{
    [Fact]
    public async Task Leader_Appends_Successfully()
    {
        // Arrange
        var fauxNode = Substitute.For<INode>();
        fauxNode.State = NodeState.Leader;
        var node = new Node([fauxNode]) { State = NodeState.Follower };
        fauxNode.Id = node.Id + 1;

        // Act
        await node.AppendEntriesRequest("12345", fauxNode.Id);

        // Assert
        await fauxNode.Received(1).AppendEntriesCommitted();
        node.Entries.Should().BeEquivalentTo(["12345"]);
    }
}