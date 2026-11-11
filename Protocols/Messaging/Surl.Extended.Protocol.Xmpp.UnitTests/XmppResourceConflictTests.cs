using System.Net;
using System.Text;
using Surl.Protocol.Abstractions;
using static Surl.Extended.Protocol.Xmpp.UnitTests.XmppLoginScript;

namespace Surl.Extended.Protocol.Xmpp.UnitTests;

/// <summary>
/// The rules BL-885 added to ADR-0114 decisions 3, 4 and 7: a resource taken over by a newer stream
/// (RFC 6120 7.7.2.2), at most ten resources a bare JID (<c>resource-constraint</c>), an authzid
/// that is not the account's (6.5.6) and a login that must finish within twice the head timeout.
/// Every expected byte is written by hand from RFC 6120 sections 4.9.3, 6.5.6, 7.6 and 7.7.
/// </summary>
[TestClass]
public sealed class XmppResourceConflictTests
{
    private const string Domain = "im.example.com";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ServeAsync_SecondStreamBindsTheSameResource_TakesItOverAndTheFirstGetsConflict()
    {
        var server = NewServer();
        var first = await OpenAsync(server, "user", "balcony", new ManualTimeProvider());

        var second = await OpenAsync(server, "user", "balcony", new ManualTimeProvider());
        await first.Serving;

        StringAssert.EndsWith(first.Connection.Written, ErrorAndClose("conflict"));
        StringAssert.EndsWith(second.Connection.Written, "<jid>user@im.example.com/balcony</jid></bind></iq>");
        CollectionAssert.Contains(first.Log.Notes.ToList(), "The stream was ended; answered the stream error conflict and closed.");
        second.Connection.Close();
        await second.Serving;
    }

    [TestMethod]
    public async Task ServeAsync_EleventhResourceOfOneBareJid_IsResourceConstraintOfTypeWait()
    {
        var server = NewServer();
        var streams = new List<TestStream>();
        for (var number = 0; number < XmppSessionRegistry.MaxResourcesPerBareJid; number++)
        {
            streams.Add(await OpenAsync(server, "user", $"r{number}", new ManualTimeProvider()));
        }

        var eleventh = await OpenAsync(server, "user", "r10", new ManualTimeProvider(), waitFor: "resource-constraint");

        StringAssert.EndsWith(
            eleventh.Connection.Written,
            "<iq type='error' id='b1'><error type='wait'><resource-constraint xmlns='urn:ietf:params:xml:ns:xmpp-stanzas'/></error></iq>");
        CollectionAssert.Contains(
            eleventh.Log.Notes.ToList(), "user@im.example.com already has 10 resources bound; answered resource-constraint.");
        streams.Add(eleventh);
        foreach (var stream in streams)
        {
            stream.Connection.Close();
            await stream.Serving;
        }
    }

    [TestMethod]
    public async Task ServeAsync_ResourceOfAnotherAccountOrAFreeOne_IsBoundBesideTheTenOfOneAccount()
    {
        var server = NewServer();
        var streams = new List<TestStream>();
        for (var number = 0; number < XmppSessionRegistry.MaxResourcesPerBareJid; number++)
        {
            streams.Add(await OpenAsync(server, "user", $"r{number}", new ManualTimeProvider()));
        }

        streams.Add(await OpenAsync(server, "other", "r0", new ManualTimeProvider()));
        streams.Add(await OpenAsync(server, "user", "r3", new ManualTimeProvider()));

        foreach (var stream in streams)
        {
            stream.Connection.Close();
            await stream.Serving;
        }
    }

    [TestMethod]
    public async Task ServeAsync_PlainAuthzidOfAnotherAccount_IsInvalidAuthzid()
    {
        var policy = PlainPolicy();

        var (answers, _) = await ServeAsync(
            Options(policy), TestContext.CancellationToken, ClientHeader, Auth("PLAIN", Base64("other@im.example.com\0user\0pencil")), Close);

        StringAssert.Contains(answers, Failure("invalid-authzid"));
        Assert.IsFalse(answers.Contains(Success, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ServeAsync_PlainAuthzidOfTheAccountsOwnBareJid_LogsInWithTheAuthzidDropped()
    {
        var policy = PlainPolicy();

        var (answers, _) = await ServeAsync(
            Options(policy), TestContext.CancellationToken, ClientHeader, Auth("PLAIN", Base64("user@im.example.com\0user\0pencil")), Close);

        StringAssert.Contains(answers, Success);
        Assert.AreEqual("\0user\0pencil", Encoding.UTF8.GetString(policy.Messages.Single()!));
    }

    [TestMethod]
    public async Task ServeAsync_PlainAuthzidSentInTheResponse_IsJudgedToo()
    {
        var policy = new UnitTestXmppSaslPolicy([], ["PLAIN"])
        {
            Answer = message => message is null
                ? new SaslLoginStep(SaslLoginOutcome.Challenge, ReadOnlyMemory<byte>.Empty, null, null)
                : UnitTestXmppSaslPolicy.AcceptedUnchecked,
        };

        var (answers, _) = await ServeAsync(
            Options(policy), TestContext.CancellationToken, ClientHeader, Auth("PLAIN", string.Empty), Response(Base64("other@im.example.com\0user\0pencil")), Close);

        StringAssert.Contains(answers, Failure("invalid-authzid"));
    }

    [TestMethod]
    [DataRow("n,a=other,n=user,r=fyko+d2lbbFgONRv9qkxdawL", true)]
    [DataRow("n,a=user@im.example.com,n=user,r=fyko+d2lbbFgONRv9qkxdawL", true)]
    [DataRow("n,a=user,n=user,r=fyko+d2lbbFgONRv9qkxdawL", false)]
    [DataRow("n,,n=user,r=fyko+d2lbbFgONRv9qkxdawL", false)]
    public async Task ServeAsync_ScramAuthzid_IsInvalidAuthzidUnlessEmptyOrTheUser(string clientFirst, bool isInvalid)
    {
        var policy = new UnitTestXmppSaslPolicy([], ["SCRAM-SHA-256"])
        {
            Answer = _ => new SaslLoginStep(SaslLoginOutcome.RefusedCredentials, ReadOnlyMemory<byte>.Empty, null, null),
        };

        var (answers, _) = await ServeAsync(
            Options(policy), TestContext.CancellationToken, ClientHeader, Auth("SCRAM-SHA-256", Base64(clientFirst)), Close);

        StringAssert.Contains(answers, Failure(isInvalid ? "invalid-authzid" : "not-authorized"));
        Assert.AreEqual(1, policy.Messages.Count);
    }

    [TestMethod]
    [DataRow("\0user\0pencil", true, "\0user\0pencil")]
    [DataRow("user\0user\0pencil", true, "user\0user\0pencil")]
    [DataRow("user@im.example.com\0user\0pencil", true, "\0user\0pencil")]
    [DataRow("other\0user\0pencil", false, "other\0user\0pencil")]
    [DataRow("other\0\0pencil", false, "other\0\0pencil")]
    [DataRow("no nul at all", true, "no nul at all")]
    [DataRow("one\0nul", true, "one\0nul")]
    public void IsAcceptablePlain_Message_IsJudgedAndRewrittenAsRfc4616And6120Say(string message, bool isAcceptable, string forwarded)
    {
        var result = XmppAuthzid.IsAcceptablePlain(Encoding.UTF8.GetBytes(message), Domain, new CountingRandomSource(), out var sent);

        Assert.AreEqual(isAcceptable, result);
        Assert.AreEqual(forwarded, Encoding.UTF8.GetString(sent));
    }

    [TestMethod]
    [DataRow("n,,n=user,r=abc", true)]
    [DataRow("n", true)]
    [DataRow("n,a=us=2Cer,n=us=2Cer,r=abc", true)]
    [DataRow("n,a=us=3Der,n=us=3Der,r=abc", true)]
    [DataRow("n,a=user,n=user,r=abc", true)]
    [DataRow("n,a=other,n=user,r=abc", false)]
    [DataRow("n,a=user,r=abc", false)]
    public void IsAcceptableScram_ClientFirstMessage_IsJudgedByItsAuthzidAndUser(string message, bool isAcceptable)
    {
        Assert.AreEqual(isAcceptable, XmppAuthzid.IsAcceptableScram(Encoding.UTF8.GetBytes(message)));
    }

    [TestMethod]
    public async Task ServeAsync_NoResourceBoundWithinTwiceTheHeadTimeout_IsPolicyViolation()
    {
        var clock = new ManualTimeProvider();
        var connection = new UnitTestXmppClientConnection();
        var log = new RecordingExchangeLog();
        connection.Send(ClientHeader);
        var serving = new XmppServer(NoCertificateOptions()).ServeAsync(connection, Context(log, clock, ExchangeLimits.Default));
        await WaitUntilAsync(() => connection.Written.Contains("</stream:features>", StringComparison.Ordinal));

        clock.Advance(ExchangeLimits.Default.HeadTimeout * 2);
        await serving;

        StringAssert.EndsWith(connection.Written, ErrorAndClose("policy-violation"));
    }

    [TestMethod]
    public async Task ServeAsync_DeadlinePassesWhileWaitingForTheHeaderAfterLogin_IsPolicyViolation()
    {
        var clock = new ManualTimeProvider();
        var connection = new UnitTestXmppClientConnection();
        connection.Send(ClientHeader);
        var serving = new XmppServer(NoCertificateOptions()).ServeAsync(connection, Context(new RecordingExchangeLog(), clock, ExchangeLimits.Default));
        await WaitUntilAsync(() => connection.Written.Contains("</stream:features>", StringComparison.Ordinal));
        clock.Advance(ExchangeLimits.Default.HeadTimeout * 1.5);
        connection.Send(Auth("PLAIN", PlainUserPencil));
        await WaitUntilAsync(() => connection.Written.EndsWith(Success, StringComparison.Ordinal) && clock.PendingTimerCount >= 2);

        clock.Advance(ExchangeLimits.Default.HeadTimeout * 0.5);
        await serving;

        StringAssert.EndsWith(connection.Written, ErrorAndClose("policy-violation"));
    }

    [TestMethod]
    public async Task ServeAsync_NoHeadTimeout_SetsNoLoginDeadline()
    {
        var clock = new ManualTimeProvider();
        var connection = new UnitTestXmppClientConnection();
        connection.Send(ClientHeader);
        var limits = ExchangeLimits.Default with { HeadTimeout = Timeout.InfiniteTimeSpan };
        var serving = new XmppServer(NoCertificateOptions()).ServeAsync(connection, Context(new RecordingExchangeLog(), clock, limits));
        await WaitUntilAsync(() => connection.Written.Contains("</stream:features>", StringComparison.Ordinal));

        clock.Advance(TimeSpan.FromDays(1));
        connection.Send(Close);
        await serving;

        StringAssert.EndsWith(connection.Written, Close);
        Assert.IsFalse(connection.Written.Contains("policy-violation", StringComparison.Ordinal));
    }

    private static XmppServerOptions NoCertificateOptions() => new()
    {
        RandomSource = new CountingRandomSource(),
        AuthenticationPolicy = new UnitTestXmppSaslPolicy(["PLAIN"], ["PLAIN"])
        {
            Answer = message =>
            {
                var user = Encoding.UTF8.GetString(message!).Split('\0')[1];
                return new SaslLoginStep(SaslLoginOutcome.Accepted, ReadOnlyMemory<byte>.Empty, user, new CheckedLogin("PLAIN", user, true));
            },
        },
    };

    private static XmppServer NewServer() => new(NoCertificateOptions());

    private ExchangeContext Context(RecordingExchangeLog log, TimeProvider clock, ExchangeLimits limits) => new(
        1,
        new ListenUrl("xmpp", Domain, 5222).WithBoundPort(5222),
        new IPEndPoint(IPAddress.Loopback, 5222),
        new IPEndPoint(IPAddress.Loopback, 50000),
        log,
        clock,
        TestContext.CancellationToken)
    {
        Limits = limits,
    };

    private async Task<TestStream> OpenAsync(XmppServer server, string user, string resource, ManualTimeProvider clock, string? waitFor = null)
    {
        var connection = new UnitTestXmppClientConnection();
        var log = new RecordingExchangeLog();
        var serving = server.ServeAsync(connection, Context(log, clock, ExchangeLimits.Default));
        connection.Send(ClientHeader + Auth("PLAIN", Base64($"\0{user}\0pencil")));
        await WaitUntilAsync(() => connection.Written.Contains(Success, StringComparison.Ordinal));
        connection.Send(ClientHeader + $"<iq id='b1' type='set'><bind xmlns='urn:ietf:params:xml:ns:xmpp-bind'><resource>{resource}</resource></bind></iq>");
        await WaitUntilAsync(() => connection.Written.Contains(waitFor ?? "</jid></bind></iq>", StringComparison.Ordinal));

        return new TestStream(connection, serving, log);
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("The server did not reach the expected state in time.");
            }

            await Task.Delay(5, TestContext.CancellationToken);
        }
    }

    private sealed record TestStream(UnitTestXmppClientConnection Connection, Task Serving, RecordingExchangeLog Log);
}
