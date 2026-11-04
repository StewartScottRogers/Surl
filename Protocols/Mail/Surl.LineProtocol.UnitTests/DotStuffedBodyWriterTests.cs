using static Surl.LineProtocol.Wire;

namespace Surl.LineProtocol;

[TestClass]
public sealed class DotStuffedBodyWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(".a\r\n", "..a\r\n.\r\n", DisplayName = "A first line starting with a dot is stuffed")]
    [DataRow("a\r\n.b\r\n..\r\n", "a\r\n..b\r\n...\r\n.\r\n", DisplayName = "Every line after CRLF starting with a dot is stuffed")]
    [DataRow("a\n.b\r\n", "a\n.b\r\n.\r\n", DisplayName = "A dot after a bare LF is not stuffed")]
    [DataRow("a\r\n.", "a\r\n..\r\n.\r\n", DisplayName = "A last line with no CRLF gets one")]
    [DataRow("abc", "abc\r\n.\r\n", DisplayName = "A message with no CRLF gets one")]
    [DataRow("", ".\r\n", DisplayName = "An empty message is the terminator alone")]
    public async Task WriteAsync_StuffsAndTerminates(string message, string expected)
    {
        var connection = Connection();

        await DotStuffedBodyWriter.WriteAsync(connection, Bytes(message), TestContext.CancellationToken);

        Assert.AreEqual(expected, Text(connection.WrittenBytes));
    }

    [TestMethod]
    public async Task WriteAsync_WhatItWrites_ReadsBackUnstuffed()
    {
        var message = ".\r\n..\r\nline\r\n.x\r\n";
        var written = Connection();
        await DotStuffedBodyWriter.WriteAsync(written, Bytes(message), TestContext.CancellationToken);
        using var reader = Reader(Connection(Text(written.WrittenBytes)));
        var body = new MemoryStream();

        await reader.ReadDotStuffedBodyAsync(body, TestContext.CancellationToken);

        Assert.AreEqual(message, Text(body.ToArray()));
    }

    [TestMethod]
    public void WriteAsync_NullConnection_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => DotStuffedBodyWriter.WriteAsync(null!, Bytes("a"), TestContext.CancellationToken));
    }
}
