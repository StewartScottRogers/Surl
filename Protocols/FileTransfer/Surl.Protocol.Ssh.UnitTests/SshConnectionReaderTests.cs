using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshConnectionReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadExactlyAsync_MoreThanTwiceTheInitialAllocation_ReadsEveryByteAcrossReads()
    {
        var sent = Enumerable.Range(0, 140000).Select(index => (byte)index).ToArray();
        var reader = new SshConnectionReader(new InMemoryConnection([sent]));

        var first = await reader.ReadByteAsync(TestContext.CancellationToken);
        var rest = await reader.ReadExactlyAsync(sent.Length - 1, TestContext.CancellationToken);

        Assert.AreEqual(0, first);
        CollectionAssert.AreEqual(sent[1..], rest);
    }

    [TestMethod]
    public async Task ReadExactlyAsync_ClosedFirst_ReturnsNull()
    {
        var reader = new SshConnectionReader(new InMemoryConnection([new byte[] { 1, 2 }]));

        var bytes = await reader.ReadExactlyAsync(3, TestContext.CancellationToken);

        Assert.IsNull(bytes);
    }

    [TestMethod]
    public async Task ReadByteAsync_Closed_ReturnsMinusOne()
    {
        var reader = new SshConnectionReader(new InMemoryConnection([]));

        var value = await reader.ReadByteAsync(TestContext.CancellationToken);

        Assert.AreEqual(-1, value);
    }
}
