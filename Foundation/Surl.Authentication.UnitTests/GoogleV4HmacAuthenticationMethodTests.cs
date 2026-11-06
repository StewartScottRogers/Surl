using System.Security.Cryptography;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// Cloud Storage's V4 HMAC check (ADR-0110 decisions 3.3 and 3.4), reached as the <c>gs</c> server
/// reaches it: a request naming <c>storage</c> as its <see cref="HttpAuthenticationRequest.AwsSigV4Service"/>,
/// judged by an <see cref="IHttpAuthenticationSession"/>. The signed GET is worked by hand from
/// Cloud Storage's "V4 signing process" and "Canonical requests" pages, every intermediate value
/// below computed independently of this library (PowerShell, <c>SHA256.HashData</c> and
/// <c>HMACSHA256.HashData</c>).
/// </summary>
[TestClass]
public sealed class GoogleV4HmacAuthenticationMethodTests
{
    // A made-up HMAC key, deliberately not shaped like a real one (GOOG + 20 characters, a
    // 40-character base64 secret): GitHub push protection refuses a commit holding that shape.
    private const string AccessId = "SURL-TEST-ACCESS-ID";

    private const string Secret = "surl-test-secret-not-a-real-hmac-key";

    private const string RequestTime = "20191201T190859Z";

    private const string Scope = "20191201/us-central1/storage/goog4_request";

    // The worked GET, by hand:
    //
    // 1. Canonical request (method, path encoded once, empty query, each signed header as
    //    name:value with a line feed after each, the signed header list, the payload hash):
    //      GET
    //      /example-bucket/cat-pics/tabby.jpeg
    //
    //      host:storage.googleapis.com
    //      x-goog-content-sha256:UNSIGNED-PAYLOAD
    //      x-goog-date:20191201T190859Z
    //
    //      host;x-goog-content-sha256;x-goog-date
    //      UNSIGNED-PAYLOAD
    //    Its SHA-256: 926f3db0599d67ff53c8efb33be6229376134cead358f05d8d567135f2bd1761
    //
    // 2. String to sign: GOOG4-HMAC-SHA256, the request time, the scope, the hash above:
    //      GOOG4-HMAC-SHA256
    //      20191201T190859Z
    //      20191201/us-central1/storage/goog4_request
    //      926f3db0599d67ff53c8efb33be6229376134cead358f05d8d567135f2bd1761
    //
    // 3. Signing key, HMAC-SHA256 chained from "GOOG4" + secret through each scope part:
    //      date     = HMAC("GOOG4surl-test-secret-not-a-real-hmac-key", "20191201")
    //               = 5f68676fde878e11dae0b2823e8dd92a63411580efa1415f98278495afe85b67
    //      location = HMAC(date, "us-central1")
    //               = ae51114c4a598bacc2ddbc7ae5c2e5b70f4ca8fe43cbce1f4145d20297545008
    //      service  = HMAC(location, "storage")
    //               = ea76dd0a503d56a093d33e9ad4539ac76bf0a830c26d1aadb8aa396d31217d98
    //      signing  = HMAC(service, "goog4_request")
    //               = e11f30a86b0a1ed4fc4631d8a5d17a8feeef1ee739070b57faa2225a988b1c77
    //
    // 4. Signature = hex(HMAC(signing, string to sign)):
    private const string WorkedSignature = "05f28f31a518575b2719182d31d951002697cb0023c10e9a77ea66ca7fb79773";

    private const string SignedHeaders = "host;x-goog-content-sha256;x-goog-date";

    private static readonly DateTimeOffset WorkedDate = new(2019, 12, 1, 19, 8, 59, TimeSpan.Zero);

    private static readonly AccountBook Accounts = new([new Account(AccessId, Secret)]);

    private readonly ManualTimeProvider clock = new();

    private static string Authorization(
        string signature = WorkedSignature,
        string accessId = AccessId,
        string scope = Scope,
        string signedHeaders = SignedHeaders,
        string scheme = "GOOG4-HMAC-SHA256") =>
        $"{scheme} Credential={accessId}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}";

    private static HttpAuthenticationRequest WorkedGet(string? authorization, params KeyValuePair<string, string>[] extraFields) =>
        new(
            "GET",
            "/example-bucket/cat-pics/tabby.jpeg",
            false,
            [
                new("Host", "storage.googleapis.com"),
                .. authorization is null ? [] : new[] { new KeyValuePair<string, string>("Authorization", authorization) },
                new("x-goog-content-sha256", "UNSIGNED-PAYLOAD"),
                new("x-goog-date", RequestTime),
                .. extraFields,
            ],
            "storage");

    private static HttpAuthenticationRequest Without(HttpAuthenticationRequest request, string name) =>
        request with { Fields = [.. request.Fields.Where(field => field.Key != name)] };

    private static HttpAuthenticationRequest With(HttpAuthenticationRequest request, string name, string value) =>
        request with { Fields = [.. Without(request, name).Fields, new KeyValuePair<string, string>(name, value)] };

    // Signs a request other than the worked one with the shared arithmetic, which the worked
    // example pins: the body binding and the Date fallback need signatures of their own.
    private static HttpAuthenticationRequest Signed(HttpAuthenticationRequest unsigned, string signedHeaders, string payloadHash, string requestTime = RequestTime)
    {
        var authorization = AwsSigV4Authorization.TryParse(
            $"Credential={AccessId}/{Scope}, SignedHeaders={signedHeaders}, Signature={new string('0', 64)}")!;
        var canonicalRequest = AwsSigV4CanonicalRequest.Build(unsigned, authorization, payloadHash, reencodesPath: false)!;
        var signature = AwsSigV4Calculation.ComputeSignature(authorization, requestTime, canonicalRequest, Encoding.UTF8.GetBytes(Secret));

        return With(unsigned, "Authorization", Authorization(Convert.ToHexStringLower(signature), signedHeaders: signedHeaders));
    }

    private static HttpAuthenticationRequest Put(string body)
    {
        var bodyHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(body)));
        var unsigned = new HttpAuthenticationRequest(
            "PUT",
            "/example-bucket/hello.txt",
            true,
            [
                new("Host", "storage.googleapis.com"),
                new("Content-Length", body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("x-goog-content-sha256", bodyHash),
                new("x-goog-date", RequestTime),
            ],
            "storage");

        return Signed(unsigned, SignedHeaders, bodyHash);
    }

    // Only Basic accepted, so --auth plays no part in the service's check.
    private AuthenticationPolicy CreatePolicy(bool allowAnonymous = false, TimeSpan clockOffset = default)
    {
        clock.Advance(WorkedDate + clockOffset - clock.GetUtcNow());

        return PolicyFixture.Create(
            Accounts,
            clock,
            allowAnonymous,
            acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Basic },
            httpMethods: [new BasicAuthenticationMethod(Accounts)]);
    }

    private async Task<HttpAuthenticationVerdict> JudgeAsync(AuthenticationPolicy policy, HttpAuthenticationRequest request)
    {
        var judgement = policy.StartHttpConnection(null).JudgeAsync(request, CancellationToken.None).AsTask();
        if (!judgement.IsCompleted)
        {
            clock.Advance(AuthenticationPolicy.RefusalDelay);
        }

        return await judgement;
    }

    private static void AssertForbidden(HttpAuthenticationVerdict verdict, HttpAuthenticationRefusal refusal)
    {
        Assert.AreEqual(HttpAuthenticationOutcome.Forbidden, verdict.Outcome);
        Assert.AreEqual(refusal, verdict.Refusal);
        Assert.IsNull(verdict.AccountName);
    }

    [TestMethod]
    public async Task VerifyAsync_HandWorkedSignedGetAtItsDate_IsAcceptedAsTheAccessIdsAccount()
    {
        var verdict = await JudgeAsync(CreatePolicy(), WorkedGet(Authorization()));

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.AreEqual(AccessId, verdict.AccountName);
        Assert.IsNull(verdict.Refusal);
        Assert.IsNull(verdict.BodyCheck);
    }

    [TestMethod]
    public async Task VerifyAsync_SchemeInLowerCase_IsAccepted()
    {
        var verdict = await JudgeAsync(CreatePolicy(), WorkedGet(Authorization(scheme: "goog4-hmac-sha256")));

        Assert.AreEqual(AccessId, verdict.AccountName);
    }

    [TestMethod]
    public async Task VerifyAsync_BadSignature_IsForbiddenAsBadSignature()
    {
        var verdict = await JudgeAsync(CreatePolicy(), WorkedGet(Authorization(signature: new string('0', 64))));

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
    }

    [TestMethod]
    public async Task VerifyAsync_ChangedSignedHost_IsForbiddenAsBadSignature()
    {
        var verdict = await JudgeAsync(CreatePolicy(), With(WorkedGet(Authorization()), "Host", "example-bucket.storage.googleapis.com"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
    }

    [TestMethod]
    public async Task VerifyAsync_UnknownAccessId_IsForbiddenAsUnknownAccountAfterTheRefusalDelay()
    {
        var judgement = CreatePolicy().StartHttpConnection(null)
            .JudgeAsync(WorkedGet(Authorization(accessId: "SURL-UNKNOWN-ACCESS-ID")), CancellationToken.None).AsTask();
        Assert.IsFalse(judgement.IsCompleted);

        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var verdict = await judgement;

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnknownAccount);
    }

    [TestMethod]
    [DataRow(16, DisplayName = "16 minutes after")]
    [DataRow(-16, DisplayName = "16 minutes before")]
    public async Task VerifyAsync_DateOutsideTheAllowedSkew_IsForbiddenAsClockSkew(int minutes)
    {
        var verdict = await JudgeAsync(CreatePolicy(clockOffset: TimeSpan.FromMinutes(minutes)), WorkedGet(Authorization()));

        AssertForbidden(verdict, HttpAuthenticationRefusal.ClockSkew);
    }

    [TestMethod]
    public async Task VerifyAsync_DateFifteenMinutesOff_IsAccepted()
    {
        var verdict = await JudgeAsync(CreatePolicy(clockOffset: TimeSpan.FromMinutes(-15)), WorkedGet(Authorization()));

        Assert.AreEqual(AccessId, verdict.AccountName);
    }

    [TestMethod]
    public async Task VerifyAsync_UnderAllowAnonymous_IsStillChecked()
    {
        var verdict = await JudgeAsync(CreatePolicy(allowAnonymous: true), WorkedGet(Authorization(signature: new string('0', 64))));

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
    }

    [TestMethod]
    [DataRow("AWS4-HMAC-SHA256", DisplayName = "AWS Signature Version 4")]
    [DataRow("OSC4-HMAC-SHA256", DisplayName = "another provider")]
    public async Task VerifyAsync_AnotherSignatureVersion4Scheme_IsForbiddenAsUnsupportedScheme(string scheme)
    {
        var verdict = await JudgeAsync(CreatePolicy(), WorkedGet(Authorization(scheme: scheme)));

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnsupportedScheme);
    }

    [TestMethod]
    [DataRow("20191201/us-central1/s3/goog4_request", DisplayName = "another service")]
    [DataRow("20191201/us-central1/storage/aws4_request", DisplayName = "another terminator")]
    public async Task VerifyAsync_ScopeNotEndingStorageGoog4Request_IsForbiddenAsMalformed(string scope)
    {
        var verdict = await JudgeAsync(CreatePolicy(), WorkedGet(Authorization(scope: scope)));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
    }

    [TestMethod]
    public async Task VerifyAsync_HostNotSigned_IsForbiddenAsMalformed()
    {
        var verdict = await JudgeAsync(CreatePolicy(), WorkedGet(Authorization(signedHeaders: "x-goog-content-sha256;x-goog-date")));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
    }

    [TestMethod]
    [DataRow("GOOG4-HMAC-SHA256", DisplayName = "no credentials")]
    [DataRow("GOOG4-HMAC-SHA256 Credential=SURL-TEST-ACCESS-ID/20191201/us-central1/storage/goog4_request", DisplayName = "parameters missing")]
    public async Task VerifyAsync_UnparsableCredentials_AreForbiddenAsMalformed(string authorization)
    {
        var verdict = await JudgeAsync(CreatePolicy(), WorkedGet(authorization));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
    }

    [TestMethod]
    public async Task VerifyAsync_NeitherGoogDateNorDate_IsForbiddenAsMissingRequestDate()
    {
        var verdict = await JudgeAsync(CreatePolicy(), Without(WorkedGet(Authorization()), "x-goog-date"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MissingRequestDate);
    }

    [TestMethod]
    public async Task VerifyAsync_NoContentHash_IsForbiddenAsMissingPayloadHash()
    {
        var verdict = await JudgeAsync(CreatePolicy(), Without(WorkedGet(Authorization()), "x-goog-content-sha256"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MissingPayloadHash);
    }

    [TestMethod]
    public async Task VerifyAsync_DateNotYyyyMMddTHHmmssZ_IsForbiddenAsMalformed()
    {
        var verdict = await JudgeAsync(
            CreatePolicy(), With(WorkedGet(Authorization()), "x-goog-date", "Sun, 01 Dec 2019 19:08:59 GMT"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
    }

    // ADR-0110 decision 3.4: a date that is not the scope's day is RequestTimeTooSkewed, even
    // within the 15-minute window; the request's date is the worked example's, the scope a day earlier.
    [TestMethod]
    public async Task VerifyAsync_DateNotTheScopesDayButWithinTheWindow_IsForbiddenAsClockSkew()
    {
        var verdict = await JudgeAsync(
            CreatePolicy(), WorkedGet(Authorization(scope: "20191130/us-central1/storage/goog4_request")));

        AssertForbidden(verdict, HttpAuthenticationRefusal.ClockSkew);
    }

    [TestMethod]
    public async Task VerifyAsync_DateFieldWithoutGoogDate_IsTheRequestTime()
    {
        var unsigned = With(Without(WorkedGet(null), "x-goog-date"), "Date", RequestTime);
        var request = Signed(unsigned, "host;x-goog-content-sha256", "UNSIGNED-PAYLOAD");

        var verdict = await JudgeAsync(CreatePolicy(), request);

        Assert.AreEqual(AccessId, verdict.AccountName);
    }

    [TestMethod]
    public async Task VerifyAsync_SignedPutWhoseBodyHashesToTheContentHash_IsServedAsTheAccount()
    {
        var verdict = await JudgeAsync(CreatePolicy(), Put("hello"));

        var bodyVerdict = await verdict.BodyCheck!.JudgeBodyAsync(SHA256.HashData("hello"u8), CancellationToken.None);

        Assert.AreEqual(AccessId, verdict.AccountName);
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, bodyVerdict.Outcome);
        Assert.AreEqual(AccessId, bodyVerdict.AccountName);
    }

    [TestMethod]
    public async Task VerifyAsync_SignedPutWhoseBodyDoesNotHashToTheContentHash_IsForbiddenAsPayloadHashMismatch()
    {
        var verdict = await JudgeAsync(CreatePolicy(), Put("hello"));

        var judgement = verdict.BodyCheck!.JudgeBodyAsync(SHA256.HashData("HELLO"u8), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var bodyVerdict = await judgement;

        AssertForbidden(bodyVerdict, HttpAuthenticationRefusal.PayloadHashMismatch);
    }

    [TestMethod]
    public async Task VerifyAsync_SignedEmptyPut_IsAcceptedOnTheHead()
    {
        var verdict = await JudgeAsync(CreatePolicy(), Put(string.Empty));

        Assert.AreEqual(AccessId, verdict.AccountName);
        Assert.IsNull(verdict.BodyCheck);
    }

    [TestMethod]
    public async Task VerifyAsync_Cancelled_Throws()
    {
        var verifier = new GoogleV4HmacAuthenticationMethod(Accounts, clock);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => verifier.VerifyAsync(string.Empty, WorkedGet(null), new CancellationToken(true)).AsTask());
    }
}
