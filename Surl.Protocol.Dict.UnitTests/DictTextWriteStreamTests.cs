using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Dict;

[TestClass]
public sealed class DictTextWriteStreamTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(new[] { "a\nb\n" }, "a\r\nb\r\n.\r\n")]
    [DataRow(new[] { "a\r\nb" }, "a\r\nb\r\n.\r\n")]
    [DataRow(new[] { "a\r", "\nb" }, "a\r\nb\r\n.\r\n")]
    [DataRow(new[] { "a\r", "b" }, "a\r\nb\r\n.\r\n")]
    [DataRow(new[] { "a\r" }, "a\r\n.\r\n")]
    [DataRow(new[] { "a\r", ".b" }, "a\r\n..b\r\n.\r\n")]
    [DataRow(new[] { ".", "\n.", "." }, "..\r\n...\r\n.\r\n")]
    [DataRow(new[] { "a.b\n" }, "a.b\r\n.\r\n")]
    [DataRow(new string[0], ".\r\n")]
    public async Task WriteAsync_Chunks_SendsCrlfLinesDotStuffedAndEndedByADotLine(string[] chunks, string expected)
    {
        var connection = new InMemoryConnection([]);
        var stream = new DictTextWriteStream(connection);

        foreach (var chunk in chunks)
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes(chunk), TestContext.CancellationToken);
        }

        await stream.EndTextAsync(TestContext.CancellationToken);

        Assert.AreEqual(expected, Encoding.ASCII.GetString(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task WriteAsync_ArrayOffsetAndCount_SendsThatSlice()
    {
        var connection = new InMemoryConnection([]);
        var stream = new DictTextWriteStream(connection);

        await stream.WriteAsync("xx.a\nyy"u8.ToArray(), 2, 3, TestContext.CancellationToken);
        await stream.FlushAsync(TestContext.CancellationToken);
        stream.Flush();

        Assert.AreEqual("..a\r\n", Encoding.ASCII.GetString(connection.WrittenBytes));
    }

    [TestMethod]
    public void Capabilities_AreWriteOnly()
    {
        var stream = new DictTextWriteStream(new InMemoryConnection([]));

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
    }

    [TestMethod]
    public void UnsupportedMembers_Throw()
    {
        var stream = new DictTextWriteStream(new InMemoryConnection([]));

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
    }
}
