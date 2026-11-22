using System.Text;
using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// A NATS <c>auth_token</c> login through the composed servers: the command line's <c>--user :tok</c> account, the
/// real per-scheme authentication policy and the NATS server, over <see cref="FakeListenerFactory"/> (BL-1588).
/// </summary>
[TestClass]
public sealed class NatsTokenLoginCompositionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_NatsTokenOfTheEmptyNameAccount_IsAccepted()
    {
        var (written, events) = await ConnectWithTokenAsync("tok");

        StringAssert.Contains(written, "+OK\r\n");
        Assert.DoesNotContain("-ERR", written);
        var judged = events.OfType<LoginJudgedEvent>().Single();
        Assert.IsTrue(judged.IsAccepted);
        Assert.AreEqual("bearer token", judged.Account);
        Assert.AreEqual("nats", judged.Scheme);
        Assert.AreEqual("password", judged.Mechanism);
    }

    [TestMethod]
    public async Task ServeAsync_NatsWrongToken_IsAuthorizationViolation()
    {
        var (written, events) = await ConnectWithTokenAsync("nope");

        StringAssert.Contains(written, "-ERR 'Authorization Violation'\r\n");
        Assert.DoesNotContain("+OK", written);
        Assert.IsFalse(events.OfType<LoginJudgedEvent>().Single().IsAccepted);
    }

    private async Task<(string Written, List<SurlEvent> Events)> ConnectWithTokenAsync(string token)
    {
        var connection = new FakeConnection(Encoding.ASCII.GetBytes($"CONNECT {{\"verbose\":true,\"auth_token\":\"{token}\"}}\r\n"));
        var seams = new StartTestSeams(new FakeListenerFactory
        {
            ConnectionsByScheme = new Dictionary<string, Task<FakeConnection>> { ["nats"] = Task.FromResult(connection) },
        });
        var start = await SurlServerComposition.ComposeAsync(
            Surl.Cli.CommandLineParser.Parse(["--user", ":tok", "--allow-plaintext-auth", "nats://127.0.0.1:0"], ["nats"]).CommandLine!,
            seams.Seams with { AreExtensionsTurnedOn = true },
            TestContext.CancellationToken);
        await using var serving = start.Serving ?? throw new AssertFailedException(string.Join(" ", start.Failure!.Lines));
        await using var subscription = serving.Events.Subscribe(new SurlEventSubscriptionOptions());

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var serve = serving.ServeAsync(StartTestSeams.Outputs(TextWriter.Null), stop.Token);
        await connection.Disposed.Task.WaitAsync(TestContext.CancellationToken);
        await stop.CancelAsync();
        await serve;

        List<SurlEvent> events = [];
        while (subscription.TryRead(out var surlEvent))
        {
            events.Add(surlEvent!);
        }

        return (Encoding.ASCII.GetString(connection.WrittenBytes), events);
    }
}
