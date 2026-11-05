using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Nats.UnitTests;

[TestClass]
public sealed class NatsControlLineReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadLineAsync_LinesSplitAcrossReads_ReadsEachWithoutItsEnding()
    {
        var reader = Reader(8192, "PI", "NG\r", "\nPONG\nPING\r\nPA");

        var lines = new List<string?>();
        for (var i = 0; i < 4; i++)
        {
            lines.Add(await ReadAsync(reader));
        }

        CollectionAssert.AreEqual(new string?[] { "PING", "PONG", "PING", null }, lines);
    }

    [TestMethod]
    public async Task ReadLineAsync_LineExactlyAtTheLimit_IsRead()
    {
        var reader = Reader(6, "PING\r\n");

        Assert.AreEqual("PING", await ReadAsync(reader));
    }

    [TestMethod]
    public async Task ReadLineAsync_LineOneOverTheLimit_Throws()
    {
        var reader = Reader(5, "PING\r\n");

        var violation = await Assert.ThrowsExactlyAsync<NatsProtocolViolationException>(() => ReadAsync(reader));

        Assert.AreEqual(NatsErrors.MaximumControlLineExceeded, violation.ErrorText);
    }

    [TestMethod]
    public async Task ReadLineAsync_LineWithoutItsEndingAlreadyAtTheLimit_ThrowsWithoutReadingFurther()
    {
        var reader = Reader(4, "PING", "\r\n");

        var violation = await Assert.ThrowsExactlyAsync<NatsProtocolViolationException>(() => ReadAsync(reader));

        Assert.AreEqual(NatsErrors.MaximumControlLineExceeded, violation.ErrorText);
    }

    [TestMethod]
    public async Task ReadLineAsync_LineLongerThanTheFirstBuffer_IsRead()
    {
        var long1 = new string('a', 10000);
        var reader = Reader(16384, long1 + "\r\nPING\r\n");

        Assert.AreEqual(long1, await ReadAsync(reader));
        Assert.AreEqual("PING", await ReadAsync(reader));
    }

    private async Task<string?> ReadAsync(NatsControlLineReader reader)
    {
        var line = await reader.ReadLineAsync(TestContext.CancellationToken);
        return line is null ? null : Encoding.UTF8.GetString(line);
    }

    private static NatsControlLineReader Reader(long maxLineBytes, params string[] chunks) =>
        new(new InMemoryConnection(chunks.Select(chunk => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(chunk))), maxLineBytes);
}
