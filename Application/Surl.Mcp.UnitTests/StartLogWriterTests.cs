using System.Text;

namespace Surl.Mcp;

[TestClass]
public sealed class StartLogWriterTests
{
    [TestMethod]
    public void StopKeeping_LinesWrittenBefore_ReturnsEachWholeLineWithoutItsLineEnd()
    {
        var errorOutput = new StringWriter();
        var writer = new StartLogWriter(errorOutput);
        writer.Write("first\r\nsecond\n");
        writer.Write("unfinished");

        var lines = writer.StopKeeping();

        CollectionAssert.AreEqual(new[] { "first", "second" }, lines.ToArray());
        Assert.AreEqual("first\r\nsecond\nunfinished", errorOutput.ToString());
    }

    [TestMethod]
    public void Write_AfterStopKeeping_StillCopiesToStandardErrorButKeepsNothing()
    {
        var errorOutput = new StringWriter();
        var writer = new StartLogWriter(errorOutput);
        writer.StopKeeping();

        writer.WriteLine("later");

        Assert.IsEmpty(writer.StopKeeping());
        Assert.AreEqual("later" + Environment.NewLine, errorOutput.ToString());
    }

    [TestMethod]
    public void Encoding_Always_IsUtf8() =>
        Assert.AreEqual(Encoding.UTF8, new StartLogWriter(TextWriter.Null).Encoding);
}
