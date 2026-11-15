using System.Text;
using Surl.Authentication;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server starts its HTTP login session with its exchange, so a login is judged, published
/// and refused as an <c>s3</c> login and not as an <c>http</c> one (BL-1207, ADR-0128 amendment 1).
/// </summary>
[TestClass]
public sealed class S3LoginExchangeTests
{
    private const string AccessKeyId = "AKIAIOSFODNN7EXAMPLE";

    private const string SecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    // The Amazon S3 API Reference's worked GET-object example, correctly signed at 20130524T000000Z.
    private const string SignedRequest =
        "GET /test.txt HTTP/1.1\r\nHost: examplebucket.s3.amazonaws.com\r\n"
        + "Authorization: AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,"
        + "SignedHeaders=host;range;x-amz-content-sha256;x-amz-date,Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41\r\n"
        + "Range: bytes=0-9\r\n"
        + "x-amz-content-sha256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\r\n"
        + "x-amz-date: 20130524T000000Z\r\nConnection: close\r\n\r\n";

    [TestMethod]
    public async Task ServeAsync_StartsTheLoginSessionWithTheExchangeAsS3()
    {
        var harness = new S3ServerHarness();

        await harness.SendAsync("GET", "/");

        var exchange = harness.Policy.StartedExchanges.Single();
        Assert.IsNotNull(exchange);
        Assert.AreEqual("s3", exchange.Scheme);
        Assert.AreEqual(1, exchange.ExchangeId);
    }

    [TestMethod]
    public async Task ServeAsync_RefuseLoginScopedToS3_RefusesAsAWrongSignatureAndPublishesAnS3Login()
    {
        var (response, events) = await ServeSignedRequestAsync("s3");

        StringAssert.Contains(response, "SignatureDoesNotMatch");
        var judged = events.Events.OfType<LoginJudgedEvent>().Single();
        Assert.AreEqual("s3", judged.Scheme);
        Assert.AreEqual(1, judged.ExchangeId);
        Assert.IsTrue(judged.IsRefusedByFault);
        Assert.IsFalse(judged.IsAccepted);
    }

    [TestMethod]
    public async Task ServeAsync_RefuseLoginScopedToHttp_DoesNotRefuseAnS3Login()
    {
        var (response, events) = await ServeSignedRequestAsync("http");

        Assert.IsFalse(response.Contains("SignatureDoesNotMatch", StringComparison.Ordinal));
        var judged = events.Events.OfType<LoginJudgedEvent>().Single();
        Assert.AreEqual("s3", judged.Scheme);
        Assert.IsFalse(judged.IsRefusedByFault);
        Assert.IsTrue(judged.IsAccepted);
    }

    private static async Task<(string Response, RecordingSurlEventPublisher Events)> ServeSignedRequestAsync(string refusedScheme)
    {
        var harness = new S3ServerHarness();
        var clock = new FixedTimeProvider(new DateTimeOffset(2013, 5, 24, 0, 0, 0, TimeSpan.Zero));
        var faults = new ScriptedFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Scheme = refusedScheme });
        var events = harness.Events;
        var policy = new AuthenticationPolicy(
            new AuthenticationSettings(new AccountBook([new Account(AccessKeyId, SecretAccessKey)]), false, false, AuthenticationMethods.DefaultAccepted),
            [],
            clock,
            events,
            faults);
        var server = new S3ProtocolServer(harness.Store, harness.Index, policy, clock);
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(SignedRequest)]);

        var serving = server.ServeAsync(connection, harness.Context());
        for (var turn = 0; turn < 100 && !serving.IsCompleted; turn++)
        {
            clock.Advance(AuthenticationPolicy.RefusalDelay);
            await Task.Yield();
        }

        await serving;

        return (Encoding.UTF8.GetString(connection.WrittenBytes), events);
    }
}
