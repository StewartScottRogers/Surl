using System.Text;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// How <see cref="CommandLineRunner"/> hands the command line's exchange limits
/// (<c>--head-timeout</c>, <c>--max-line</c>, <c>--max-filesize</c> and the rest of
/// <see cref="Cli.SurlCommandLine.Limits"/>) to the serving engine, so every exchange's
/// <see cref="ExchangeContext.Limits"/> holds them (BL-245). The SMTP server shows what its
/// context held: its EHLO <c>SIZE</c>, its line limit and its head timeout. No test here touches
/// the disk or the network.
/// </summary>
[TestClass]
public sealed class CommandLineRunnerExchangeLimitsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_MaxFilesize10_AdvertisesSize10InTheEhloReply()
    {
        var written = await ServeOneConnectionAsync(
            new FakeConnection(Encoding.ASCII.GetBytes("EHLO c\r\nQUIT\r\n")), "-s", "--max-filesize", "10", "smtp://127.0.0.1:0/");

        StringAssert.Contains(written, "SIZE 10\r\n");
        Assert.DoesNotContain("SIZE 104857600", written);
    }

    [TestMethod]
    public async Task RunAsync_MaxLine16_RefusesALongerCommandLine()
    {
        var written = await ServeOneConnectionAsync(
            new FakeConnection(Encoding.ASCII.GetBytes("NOOP 0123456789abcdefghij\r\nQUIT\r\n")), "-s", "--max-line", "16", "smtp://127.0.0.1:0/");

        Assert.AreEqual("220 surl ESMTP ready\r\n500 5.5.6 Command line too long\r\n", written);
    }

    [TestMethod]
    public async Task RunAsync_HeadTimeoutOfAHundredthOfASecond_AnswersAClientStalledMidLine421()
    {
        var written = await ServeOneConnectionAsync(
            new FakeConnection(Encoding.ASCII.GetBytes("NOOP\r\nNO"), stallsWhenExhausted: true),
            "-s", "--head-timeout", "0.01", "smtp://127.0.0.1:0/");

        Assert.AreEqual(
            "220 surl ESMTP ready\r\n250 2.0.0 OK\r\n421 4.4.2 surl Timeout waiting for a command, closing\r\n", written);
    }

    // Serves until the connection has ended, then stops, and returns what the server wrote.
    private async Task<string> ServeOneConnectionAsync(FakeConnection connection, params string[] args)
    {
        var factory = new FakeListenerFactory { Connection = connection };
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new CommandLineRunner(_ => factory, _ => true, _ => DataDirectoryLockOutcome.NoLock, TimeProvider.System);

        var running = runner.RunAsync(args, output, error, stop.Token);
        await factory.AcceptStarted.Task.WaitAsync(TestContext.CancellationToken);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        Assert.AreEqual(SurlExitCode.Ok, await running, error.ToString());
        return Encoding.ASCII.GetString(connection.WrittenBytes);
    }
}
