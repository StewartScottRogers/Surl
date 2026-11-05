using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// A request naming its <see cref="HttpAuthenticationRequest.AwsSigV4Service"/>, as the S3 server's
/// do (ADR-0108 decisions 4.1 to 4.4): judged by Signature Version 4 alone, whatever <c>--auth</c>
/// says, each refusal <see cref="HttpAuthenticationOutcome.Forbidden"/> with its
/// <see cref="HttpAuthenticationRefusal"/>. The signed request is the Amazon S3 API Reference's
/// worked GET-object example ("Signature Calculations for the Authorization Header: Transferring
/// Payload in a Single Chunk"), with its access key, secret and signature.
/// </summary>
[TestClass]
public sealed class AwsSigV4ServiceSignatureTests
{
    private const string ExampleAccessKeyId = "AKIAIOSFODNN7EXAMPLE";

    private const string ExampleSecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    private const string ExampleSignature = "f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41";

    private const string EmptyBodySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    // The example's x-amz-date, 20130524T000000Z.
    private static readonly DateTimeOffset ExampleDate = new(2013, 5, 24, 0, 0, 0, TimeSpan.Zero);

    private static readonly AccountBook Accounts = new([new Account(ExampleAccessKeyId, ExampleSecretAccessKey)]);

    private readonly ManualTimeProvider clock = new();

    private static string ExampleAuthorization(
        string accessKeyId = ExampleAccessKeyId, string service = "s3", string signature = ExampleSignature) =>
        $"AWS4-HMAC-SHA256 Credential={accessKeyId}/20130524/us-east-1/{service}/aws4_request,"
        + $"SignedHeaders=host;range;x-amz-content-sha256;x-amz-date,Signature={signature}";

    // GET /test.txt as the example sends it to examplebucket.s3.amazonaws.com.
    private static HttpAuthenticationRequest ExampleGetObject(string? authorization, string method = "GET", params KeyValuePair<string, string>[] extraFields) =>
        new(
            method,
            "/test.txt",
            method is not ("GET" or "HEAD"),
            [
                new("Host", "examplebucket.s3.amazonaws.com"),
                .. authorization is null ? [] : new[] { new KeyValuePair<string, string>("Authorization", authorization) },
                new("Range", "bytes=0-9"),
                new("x-amz-content-sha256", EmptyBodySha256),
                new("x-amz-date", "20130524T000000Z"),
                .. extraFields,
            ],
            "s3");

    private static HttpAuthenticationRequest Without(HttpAuthenticationRequest request, string name) =>
        request with { Fields = [.. request.Fields.Where(field => field.Key != name)] };

    private static HttpAuthenticationRequest With(HttpAuthenticationRequest request, string name, string value) =>
        request with { Fields = [.. Without(request, name).Fields, new KeyValuePair<string, string>(name, value)] };

    // Only Basic accepted, so --auth plays no part in the service's check.
    private AuthenticationPolicy CreatePolicy(bool allowAnonymous = false, TimeSpan clockOffset = default)
    {
        clock.Advance(ExampleDate + clockOffset - clock.GetUtcNow());

        return PolicyFixture.Create(
            Accounts,
            clock,
            allowAnonymous,
            acceptedMethods: new HashSet<AuthenticationMethod> { AuthenticationMethod.Basic },
            httpMethods: [new BasicAuthenticationMethod(Accounts)]);
    }

    // Judges the request, letting the refusal delay pass when the judgement waits on it.
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
        Assert.IsEmpty(verdict.WwwAuthenticateValues);
    }

    [TestMethod]
    public async Task WorkedGetObjectExample_AtTheExamplesDate_IsAcceptedAsTheKeysAccount()
    {
        var verdict = await JudgeAsync(CreatePolicy(), ExampleGetObject(ExampleAuthorization()));

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.AreEqual(ExampleAccessKeyId, verdict.AccountName);
        Assert.IsNull(verdict.Refusal);
        Assert.IsNull(verdict.BodyCheck);
        Assert.AreEqual("Login accepted: AWS4-HMAC-SHA256 AKIAIOSFODNN7EXAMPLE", verdict.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task WorkedGetObjectExample_UnderAllowAnonymous_IsStillCheckedAndLoggedIn()
    {
        var verdict = await JudgeAsync(CreatePolicy(allowAnonymous: true), ExampleGetObject(ExampleAuthorization()));

        Assert.AreEqual(ExampleAccessKeyId, verdict.AccountName);
    }

    [TestMethod]
    public async Task NoAuthorization_AnonymousOff_IsForbiddenAsMissingCredentialsUndelayed()
    {
        var judgement = CreatePolicy().StartHttpConnection(null).JudgeAsync(ExampleGetObject(null), CancellationToken.None);

        Assert.IsTrue(judgement.IsCompleted);
        AssertForbidden(await judgement, HttpAuthenticationRefusal.MissingCredentials);
    }

    [TestMethod]
    [DataRow("GET")]
    [DataRow("PUT")]
    public async Task NoAuthorization_UnderAllowAnonymous_ProceedsWithNoAccount(string method)
    {
        var verdict = await JudgeAsync(CreatePolicy(allowAnonymous: true), ExampleGetObject(null, method));

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.IsNull(verdict.AccountName);
        Assert.IsNull(verdict.Refusal);
    }

    [TestMethod]
    [DataRow("Basic QUtJQU9TRk9ETk43RVhBTVBMRTp4", DisplayName = "Basic, which --auth accepts")]
    [DataRow("Bearer token", DisplayName = "Bearer")]
    [DataRow("AWS AKIAIOSFODNN7EXAMPLE:frJIUN8DYpKDtOLCwo//yllqDzg=", DisplayName = "signature version 2")]
    public async Task OtherScheme_IsForbiddenAsUnsupportedSchemeUnderAllowAnonymousToo(string authorization)
    {
        var verdict = await JudgeAsync(CreatePolicy(allowAnonymous: true), ExampleGetObject(authorization));

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnsupportedScheme);
    }

    [TestMethod]
    public async Task UnknownAccessKey_IsForbiddenAsUnknownAccountAfterTheRefusalDelay()
    {
        var judgement = CreatePolicy().StartHttpConnection(null)
            .JudgeAsync(ExampleGetObject(ExampleAuthorization(accessKeyId: "AKIAUNKNOWNEXAMPLE00")), CancellationToken.None).AsTask();
        Assert.IsFalse(judgement.IsCompleted);

        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var verdict = await judgement;

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnknownAccount);
        Assert.AreEqual("Login refused: AWS4-HMAC-SHA256 AKIAUNKNOWNEXAMPLE00", verdict.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task ChangedSignedField_IsForbiddenAsBadSignature()
    {
        var verdict = await JudgeAsync(CreatePolicy(), With(ExampleGetObject(ExampleAuthorization()), "Range", "bytes=0-10"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
    }

    [TestMethod]
    public async Task BadSignature_UnderAllowAnonymous_IsStillForbidden()
    {
        var verdict = await JudgeAsync(
            CreatePolicy(allowAnonymous: true),
            ExampleGetObject(ExampleAuthorization(signature: new string('0', 64))));

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
    }

    [TestMethod]
    [DataRow(16, DisplayName = "16 minutes after")]
    [DataRow(-16, DisplayName = "16 minutes before")]
    public async Task DateSixteenMinutesOff_IsForbiddenAsClockSkew(int minutes)
    {
        var verdict = await JudgeAsync(CreatePolicy(clockOffset: TimeSpan.FromMinutes(minutes)), ExampleGetObject(ExampleAuthorization()));

        AssertForbidden(verdict, HttpAuthenticationRefusal.ClockSkew);
    }

    [TestMethod]
    public async Task DateFifteenMinutesOff_IsAccepted()
    {
        var verdict = await JudgeAsync(CreatePolicy(clockOffset: TimeSpan.FromMinutes(15)), ExampleGetObject(ExampleAuthorization()));

        Assert.AreEqual(ExampleAccessKeyId, verdict.AccountName);
    }

    [TestMethod]
    public async Task ScopeForAnotherService_IsForbiddenAsMalformedCredentials()
    {
        var verdict = await JudgeAsync(CreatePolicy(), ExampleGetObject(ExampleAuthorization(service: "ec2")));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
    }

    [TestMethod]
    [DataRow("AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request", DisplayName = "parameters missing")]
    [DataRow("AWS4-HMAC-SHA256", DisplayName = "no credentials")]
    public async Task UnparsableCredentials_AreForbiddenAsMalformed(string authorization)
    {
        var verdict = await JudgeAsync(CreatePolicy(), ExampleGetObject(authorization));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
        Assert.AreEqual("Login refused: AWS4-HMAC-SHA256", verdict.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task SignedDateMissing_IsForbiddenAsMalformedCredentials()
    {
        var verdict = await JudgeAsync(CreatePolicy(), Without(ExampleGetObject(ExampleAuthorization()), "x-amz-date"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
    }

    [TestMethod]
    public async Task ContentHashMissing_IsForbiddenAsMissingPayloadHash()
    {
        var verdict = await JudgeAsync(CreatePolicy(), Without(ExampleGetObject(ExampleAuthorization()), "x-amz-content-sha256"));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MissingPayloadHash);
    }

    [TestMethod]
    public async Task BodyNotHashingToTheContentHash_IsForbiddenAsPayloadHashMismatchOnceRead()
    {
        // Content-Length is not signed, so the head still verifies and the body is awaited.
        var request = ExampleGetObject(ExampleAuthorization(), "GET", new KeyValuePair<string, string>("Content-Length", "5"));
        var verdict = await JudgeAsync(CreatePolicy(), request);
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);

        var judgement = verdict.BodyCheck!.JudgeBodyAsync(SHA256.HashData("hello"u8), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var bodyVerdict = await judgement;

        AssertForbidden(bodyVerdict, HttpAuthenticationRefusal.PayloadHashMismatch);
        Assert.AreEqual("Login refused: AWS4-HMAC-SHA256 AKIAIOSFODNN7EXAMPLE", bodyVerdict.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task BodyHashingToTheContentHash_IsServedAsTheKeysAccount()
    {
        var request = ExampleGetObject(ExampleAuthorization(), "GET", new KeyValuePair<string, string>("Content-Length", "5"));
        var verdict = await JudgeAsync(CreatePolicy(), request);

        var bodyVerdict = await verdict.BodyCheck!.JudgeBodyAsync(SHA256.HashData([]), CancellationToken.None);

        // The head's signature is verified, so the head's verdict names the account pending the body.
        Assert.AreEqual(ExampleAccessKeyId, verdict.AccountName);
        Assert.IsNull(verdict.CheckedLogin);
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, bodyVerdict.Outcome);
        Assert.AreEqual(ExampleAccessKeyId, bodyVerdict.AccountName);
    }

    [TestMethod]
    public async Task HttpRequest_RefusedSignature_IsChallengedCarryingItsReason()
    {
        // Without a service the HTTP rules hold: a refused SigV4 login is the usual challenge,
        // which carries the reason the HTTP server ignores.
        var request = ExampleGetObject(ExampleAuthorization(signature: new string('0', 64))) with { AwsSigV4Service = null };
        var policy = PolicyFixture.Create(Accounts, clock, httpMethods: [new AwsSigV4AuthenticationMethod(Accounts, clock), new BasicAuthenticationMethod(Accounts)]);
        clock.Advance(ExampleDate - clock.GetUtcNow());

        var judgement = policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(request, CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var verdict = await judgement;

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        Assert.AreEqual(HttpAuthenticationRefusal.BadSignature, verdict.Refusal);
    }
}
