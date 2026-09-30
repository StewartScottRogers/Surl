using static Surl.LineProtocol.Wire;

namespace Surl.LineProtocol;

[TestClass]
public sealed class ReplyLineWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_PrintableAscii_IsWrittenWithCrlf()
    {
        var connection = Connection();

        await ReplyLineWriter.WriteAsync(connection, "250 2.0.0 OK ~ !", TestContext.CancellationToken);

        Assert.AreEqual("250 2.0.0 OK ~ !\r\n", Text(connection.WrittenBytes));
    }

    [TestMethod]
    [DataRow("250 a\r\n250 b", DisplayName = "CRLF")]
    [DataRow("250 a\rb", DisplayName = "CR")]
    [DataRow("250 a\nb", DisplayName = "LF")]
    [DataRow("250 a\tb", DisplayName = "Tab")]
    [DataRow("250 a\u007Fb", DisplayName = "DEL")]
    [DataRow("250 café", DisplayName = "Non-ASCII")]
    public void WriteAsync_TextOutsidePrintableAscii_ThrowsAndWritesNothing(string text)
    {
        var connection = Connection();

        Assert.ThrowsExactly<ArgumentException>(() => ReplyLineWriter.WriteAsync(connection, text, TestContext.CancellationToken));
        Assert.IsEmpty(connection.WrittenBytes);
    }

    [TestMethod]
    public void WriteAsync_NullArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ReplyLineWriter.WriteAsync(null!, "250 OK", TestContext.CancellationToken));
        Assert.ThrowsExactly<ArgumentNullException>(() => ReplyLineWriter.WriteAsync(Connection(), null!, TestContext.CancellationToken));
    }
}
