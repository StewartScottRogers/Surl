using Surl.Output;

namespace Surl.Composition;

/// <summary><see cref="DirectoryNoteSink"/>: notes reach the log only once it is attached, from the verbose level up (ADR-0149 decision 9; BL-673).</summary>
[TestClass]
public sealed class DirectoryNoteSinkTests
{
    [TestMethod]
    public void Write_BeforeALogIsAttached_WritesNothing()
    {
        var sink = new DirectoryNoteSink();

        sink.Write("early");

        using var log = new StringWriter();
        sink.Attach(log, LogLevel.Verbose);
        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public void Write_BelowVerbose_WritesNothing()
    {
        var sink = new DirectoryNoteSink();
        using var log = new StringWriter();
        sink.Attach(log, LogLevel.Info);

        sink.Write("quiet");

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public void Write_Verbose_WritesTheLine()
    {
        var sink = new DirectoryNoteSink();
        using var log = new StringWriter();
        sink.Attach(log, LogLevel.Verbose);

        sink.Write("Login refused: password bob (directory: no such entry)");

        Assert.AreEqual("Login refused: password bob (directory: no such entry)" + Environment.NewLine, log.ToString());
    }

    [TestMethod]
    public void Attach_NullLog_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new DirectoryNoteSink().Attach(null!, LogLevel.Verbose));
}
