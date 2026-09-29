using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

[TestClass]
public sealed class RefusalWriteDeadlineTests
{
    private const string UnknownMethod = "BREW /file.txt HTTP/1.1\r\nHost: h\r\n\r\n";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RefusalThatCannotBeWritten_IsGivenUpAfterOneSecondWithoutAbort()
    {
        var clock = new ManualTimeProvider(Now);
        var connection = new StalledWriteConnection(Ascii(UnknownMethod));
        var log = new RecordingExchangeLog();
        var serving = Server().ServeAsync(connection, Context(log, clock, TestContext.CancellationToken));

        Assert.AreEqual(1, clock.ActiveTimerCount, "Only the refusal's write deadline is running.");
        clock.Advance(TimeSpan.FromSeconds(1));
        await serving;

        Assert.IsFalse(connection.Aborted);
        Assert.IsFalse(connection.WritesCompleted, "The close is left to the engine's dispose.");
        Assert.AreEqual("The 501 was not written within its 1-second write deadline; the connection is closed without it.", log.Notes[1]);
    }

    [TestMethod]
    public async Task ExchangeCancelledWhileARefusalIsWritten_Throws()
    {
        var connection = new StalledWriteConnection(Ascii(UnknownMethod));
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serving = Server().ServeAsync(connection, Context(new RecordingExchangeLog(), new ManualTimeProvider(Now), exchange.Token));

        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
        Assert.IsFalse(connection.Aborted);
    }
}
