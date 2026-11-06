using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// The byte source under the XML reader (BL-536; ADR-0114 decisions 6 and 7): what it counts and
/// keeps, and the stream members it does not support.
/// </summary>
[TestClass]
public sealed class XmppInputStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_LargeBuffer_HandsOverAtMostMaxReadBytes()
    {
        var input = Input(new string('a', 10000));
        var buffer = new byte[10000];

        var count = await input.ReadAsync(buffer, 0, buffer.Length, TestContext.CancellationToken);

        Assert.AreEqual(XmppInputStream.MaxReadBytes, count);
    }

    [TestMethod]
    public async Task RecentText_AfterManyReads_KeepsTheLastRecentBytes()
    {
        var input = Input(new string('a', XmppInputStream.RecentBytes), "tail");
        var buffer = new byte[XmppInputStream.MaxReadBytes];

        while (await input.ReadAsync(buffer, TestContext.CancellationToken) > 0)
        {
        }

        Assert.AreEqual(XmppInputStream.RecentBytes, input.RecentText.Length);
        Assert.EndsWith("aatail", input.RecentText);
        Assert.IsTrue(input.ReachedEnd);
    }

    [TestMethod]
    public async Task StartElementCount_StartsTheSizeCountAgain()
    {
        var input = Input(10, "12345678", "12345678");
        var buffer = new byte[8];
        Assert.AreEqual(8, await input.ReadAsync(buffer, TestContext.CancellationToken));

        input.StartElementCount();

        Assert.AreEqual(8, await input.ReadAsync(buffer, TestContext.CancellationToken));
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        var input = Input();

        Assert.IsTrue(input.CanRead);
        Assert.IsFalse(input.CanSeek);
        Assert.IsFalse(input.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => input.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => input.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => input.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => input.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(input.Flush);
        Assert.ThrowsExactly<NotSupportedException>(() => input.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => input.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => input.Write(new byte[1], 0, 1));
    }

    private static XmppInputStream Input(params string[] chunks) => Input(long.MaxValue, chunks);

    private static XmppInputStream Input(long maxElementBytes, params string[] chunks) => new(
        new InMemoryConnection(chunks.Select(chunk => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(chunk))), maxElementBytes);
}
