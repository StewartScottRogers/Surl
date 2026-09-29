using System.Text;

namespace Surl.Protocol.Abstractions;

[TestClass]
public sealed class ConnectionRefusalWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteRefusalAsync_WritesThroughTheConnection()
    {
        IConnectionRefusalWriter writer = new NamingRefusalWriter();
        var connection = new InMemoryConnection([]);

        await writer.WriteRefusalAsync(connection, ConnectionRefusal.TooManyConnectionsFromAddress, TestContext.CancellationToken);

        Assert.AreEqual("TooManyConnectionsFromAddress", Encoding.ASCII.GetString(connection.WrittenBytes));
    }

    [TestMethod]
    public void ConnectionRefusal_HasExactlyTheTwoNamedMembers()
    {
        var names = Enum.GetNames<ConnectionRefusal>();

        CollectionAssert.AreEqual(new[] { "TooManyConnections", "TooManyConnectionsFromAddress" }, names);
    }

    private sealed class NamingRefusalWriter : IConnectionRefusalWriter
    {
        public ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal, CancellationToken cancellationToken)
            => connection.WriteAsync(Encoding.ASCII.GetBytes(refusal.ToString()), cancellationToken);
    }
}
