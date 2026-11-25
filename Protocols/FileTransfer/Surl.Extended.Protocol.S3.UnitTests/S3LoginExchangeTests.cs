using System.Security.Cryptography;
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

    private const string ObjectContent = "hello world";

    // S3ServerHarness.Now as x-amz-date writes it.
    private const string SignedAt = "20261105T120000Z";

    // The SHA-256 of no bytes.
    private const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

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

    [TestMethod]
    public async Task ServeAsync_ValidSignatureScopedToAnotherRegionThanTheVirtualHostedRegionalHost_Is400AuthorizationHeaderMalformedWithNoObject()
    {
        var response = await ServeRegionalRequestAsync(HeaderSignedGet("photos.s3.us-east-1.amazonaws.com", "/hello.txt", "eu-west-1"));

        AssertAuthorizationHeaderMalformedWithNoObject(response);
    }

    [TestMethod]
    public async Task ServeAsync_ValidSignatureScopedToTheVirtualHostedRegionalHostsRegion_Is200()
    {
        var response = await ServeRegionalRequestAsync(HeaderSignedGet("photos.s3.us-east-1.amazonaws.com", "/hello.txt", "us-east-1"));

        AssertObjectSent(response);
    }

    [TestMethod]
    public async Task ServeAsync_PathStyleRegionalHostScopedToAnotherRegion_Is400AuthorizationHeaderMalformed()
    {
        var response = await ServeRegionalRequestAsync(HeaderSignedGet("s3.us-east-1.amazonaws.com", "/photos/hello.txt", "eu-west-1"));

        AssertAuthorizationHeaderMalformedWithNoObject(response);
    }

    [TestMethod]
    public async Task ServeAsync_PresignedUrlScopedToAnotherRegionThanTheRegionalHost_Is400AuthorizationHeaderMalformed()
    {
        var response = await ServeRegionalRequestAsync(PresignedGet("photos.s3.us-east-1.amazonaws.com", "/hello.txt", "eu-west-1"));

        AssertAuthorizationHeaderMalformedWithNoObject(response);
    }

    [TestMethod]
    public async Task ServeAsync_PresignedUrlScopedToTheRegionalHostsRegion_Is200()
    {
        var response = await ServeRegionalRequestAsync(PresignedGet("photos.s3.us-east-1.amazonaws.com", "/hello.txt", "us-east-1"));

        AssertObjectSent(response);
    }

    [TestMethod]
    [DataRow("127.0.0.1:9000", "/photos/hello.txt", DisplayName = "an IP literal")]
    [DataRow("localhost", "/photos/hello.txt", DisplayName = "localhost")]
    [DataRow("s3.amazonaws.com", "/photos/hello.txt", DisplayName = "the global endpoint")]
    [DataRow("photos.s3.amazonaws.com", "/hello.txt", DisplayName = "a bucket of the global endpoint")]
    [DataRow("s3.test", "/photos/hello.txt", DisplayName = "another DNS name")]
    public async Task ServeAsync_HostNamingNoRegion_AcceptsAScopeOfAnyRegion(string host, string path)
    {
        var response = await ServeRegionalRequestAsync(HeaderSignedGet(host, path, "eu-west-1"));

        AssertObjectSent(response);
    }

    private static void AssertAuthorizationHeaderMalformedWithNoObject(string response)
    {
        Assert.StartsWith("HTTP/1.1 400 Bad Request\r\n", response);
        Assert.Contains("<Code>AuthorizationHeaderMalformed</Code>", response);
        Assert.DoesNotContain(ObjectContent, response);
    }

    private static void AssertObjectSent(string response)
    {
        Assert.StartsWith("HTTP/1.1 200 OK\r\n", response);
        Assert.EndsWith("\r\n\r\n" + ObjectContent, response);
    }

    // photos/hello.txt served to a request signed at the harness's time, judged by the real policy.
    private static async Task<string> ServeRegionalRequestAsync(string request)
    {
        var harness = new S3ServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/hello.txt", ObjectContent);
        var policy = new AuthenticationPolicy(
            new AuthenticationSettings(new AccountBook([new Account(AccessKeyId, SecretAccessKey)]), false, false, AuthenticationMethods.DefaultAccepted),
            [],
            harness.TimeProvider,
            harness.Events,
            harness.Faults);
        var server = new S3ProtocolServer(harness.Store, harness.Index, policy, harness.TimeProvider);
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)]);

        await ServeMovingTheClockPastAnyRefusalDelayAsync(server, connection, harness.Context(), harness.TimeProvider);

        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }

    // A GET signed in its Authorization field ("Signature Calculations for the Authorization Header").
    private static string HeaderSignedGet(string host, string path, string region)
    {
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest =
            $"GET\n{path}\n\nhost:{host}\nx-amz-content-sha256:{EmptySha256}\nx-amz-date:{SignedAt}\n\n{signedHeaders}\n{EmptySha256}";
        var scope = ScopeOf(region);

        return $"GET {path} HTTP/1.1\r\nHost: {host}\r\n"
            + $"Authorization: AWS4-HMAC-SHA256 Credential={AccessKeyId}/{scope},SignedHeaders={signedHeaders},Signature={SignatureOf(scope, canonicalRequest)}\r\n"
            + $"x-amz-content-sha256: {EmptySha256}\r\nx-amz-date: {SignedAt}\r\nConnection: close\r\n\r\n";
    }

    // A GET signed in its query ("Authenticating Requests: Using Query Parameters").
    private static string PresignedGet(string host, string path, string region)
    {
        var scope = ScopeOf(region);
        var query = $"X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential={Uri.EscapeDataString($"{AccessKeyId}/{scope}")}"
            + $"&X-Amz-Date={SignedAt}&X-Amz-Expires=60&X-Amz-SignedHeaders=host";
        var canonicalRequest = $"GET\n{path}\n{query}\nhost:{host}\n\nhost\nUNSIGNED-PAYLOAD";

        return $"GET {path}?{query}&X-Amz-Signature={SignatureOf(scope, canonicalRequest)} HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\n\r\n";
    }

    // S3ServerHarness.Now's date.
    private static string ScopeOf(string region) => $"20261105/{region}/s3/aws4_request";

    private static string SignatureOf(string scope, string canonicalRequest)
    {
        var stringToSign = $"AWS4-HMAC-SHA256\n{SignedAt}\n{scope}\n{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";
        var signingKey = scope.Split('/').Aggregate(
            Encoding.UTF8.GetBytes("AWS4" + SecretAccessKey),
            (key, part) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(part)));

        return Convert.ToHexStringLower(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign)));
    }

    // A refused login is held for the policy's delay, which only a moved clock ends.
    private static async Task ServeMovingTheClockPastAnyRefusalDelayAsync(S3ProtocolServer server, InMemoryConnection connection, ExchangeContext context, FixedTimeProvider clock)
    {
        var serving = server.ServeAsync(connection, context);
        for (var turn = 0; turn < 100 && !serving.IsCompleted; turn++)
        {
            clock.Advance(AuthenticationPolicy.RefusalDelay);
            await Task.Yield();
        }

        await serving;
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
