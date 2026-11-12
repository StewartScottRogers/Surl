using Surl.Output;

namespace Surl.Composition;

[TestClass]
public sealed class ReadableByOthersWarningTests
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(OwnerOnly | UnixFileMode.GroupRead)]
    [DataRow(OwnerOnly | UnixFileMode.OtherRead)]
    [DataRow(OwnerOnly | UnixFileMode.GroupRead | UnixFileMode.OtherRead)]
    public void Write_ModeGrantsGroupOrOtherRead_WritesTheWarning(UnixFileMode mode)
    {
        using var log = new StringWriter();

        ReadableByOthersWarning.Write("users.txt", mode, LogLevel.Info, log);

        Assert.AreEqual("surl: warning: users.txt is readable by other users and may hold passwords" + Environment.NewLine, log.ToString());
    }

    [TestMethod]
    [DataRow(OwnerOnly)]
    [DataRow(OwnerOnly | UnixFileMode.GroupWrite | UnixFileMode.OtherExecute)]
    public void Write_ModeGrantsNoGroupOrOtherRead_WritesNothing(UnixFileMode mode)
    {
        using var log = new StringWriter();

        ReadableByOthersWarning.Write("users.txt", mode, LogLevel.Info, log);

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public void Write_NoMode_WritesNothing()
    {
        using var log = new StringWriter();

        ReadableByOthersWarning.Write("users.txt", null, LogLevel.Info, log);

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public void Write_BelowInfo_WritesNothing()
    {
        using var log = new StringWriter();

        ReadableByOthersWarning.Write("users.txt", OwnerOnly | UnixFileMode.OtherRead, LogLevel.Error, log);

        Assert.AreEqual(string.Empty, log.ToString());
    }

    [TestMethod]
    public void Write_NullLog_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ReadableByOthersWarning.Write("users.txt", OwnerOnly, LogLevel.Info, null!));

    [TestMethod]
    public async Task ServeAsync_UserFileReadableByGroup_WritesTheWarningBeforeTheStatusLineThroughTheModeSeam()
    {
        var log = await ServeUserFileAsync(OwnerOnly | UnixFileMode.GroupRead);

        Assert.AreEqual(
            $"surl: warning: users.txt is readable by other users and may hold passwords{Environment.NewLine}"
            + $"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}",
            log);
    }

    [TestMethod]
    public async Task ServeAsync_UserFileOwnerOnly_WritesNoWarningThroughTheModeSeam()
    {
        var log = await ServeUserFileAsync(OwnerOnly);

        Assert.AreEqual($"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}", log);
    }

    [TestMethod]
    public async Task ServeAsync_ModeSeamReportsNoModeAsOnWindows_WritesNoWarning()
    {
        var log = await ServeUserFileAsync(null);

        Assert.AreEqual($"Listening on http://127.0.0.1:{FakeListenerFactory.BoundPort}/{Environment.NewLine}", log);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    public void ReadDiskMode_OnWindows_ReportsNoMode()
    {
        var path = Path.GetTempFileName();
        try
        {
            Assert.IsNull(ReadableByOthersWarning.ReadDiskMode(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void ReadDiskMode_OffWindows_ReportsTheFilesModeAndNoneForAMissingFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.SetUnixFileMode(path, OwnerOnly | UnixFileMode.OtherRead);

            Assert.AreEqual(OwnerOnly | UnixFileMode.OtherRead, ReadableByOthersWarning.ReadDiskMode(path));
            Assert.IsNull(ReadableByOthersWarning.ReadDiskMode(path + ".missing"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private async Task<string> ServeUserFileAsync(UnixFileMode? mode)
    {
        var seams = new StartTestSeams().Seams with
        {
            ReadStartFile = _ => "carol:secret\n"u8.ToArray(),
            ReadStartFileUnixMode = path => path == "users.txt" ? mode : throw new AssertFailedException(path),
        };
        var start = await SurlServerComposition.ComposeAsync(
            StartTestSeams.Parse("--user-file", "users.txt", "http://127.0.0.1:0"), seams, TestContext.CancellationToken);
        await using var serving = start.Serving ?? throw new AssertFailedException("The start was refused.");
        using var log = new StringWriter();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var bound = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serve = serving.ServeAsync(StartTestSeams.Outputs(log, _ => bound.SetResult()), stop.Token);
        await bound.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;
        return log.ToString();
    }
}
