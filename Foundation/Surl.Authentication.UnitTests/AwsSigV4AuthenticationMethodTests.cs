using System.Globalization;
using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// <see cref="AwsSigV4AuthenticationMethod"/> alone and behind <see cref="AuthenticationPolicy"/>,
/// replaying the requests pinned upstream curl 8.21.0 signed with <c>--aws-sigv4</c>
/// (Fixtures/README.md) at the time each was signed, then tampering with each signed part
/// (ADR-0043).
/// </summary>
[TestClass]
public sealed class AwsSigV4AuthenticationMethodTests
{
    private const string KeyId = "AKIDEXAMPLE";

    private static readonly AccountBook Accounts = new(
    [
        new Account(KeyId, "secret"),
        new Account("other", "secret"),
        new Account(string.Empty, "tok"),
    ]);

    private readonly ManualTimeProvider clock = new();

    // Moves the clock to the recorded request's date field plus offset.
    private void SetClockTo(HttpAuthenticationRequest request, TimeSpan offset = default)
    {
        var sent = request.Fields.Single(field => field.Key.EndsWith("-Date", StringComparison.Ordinal)).Value;
        var time = DateTimeOffset.ParseExact(
            sent, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        clock.Advance(time + offset - clock.GetUtcNow());
    }

    private async Task<HttpCredentialCheck> VerifyAsync(
        HttpAuthenticationRequest request, AccountBook? accounts = null, TimeSpan clockOffset = default)
    {
        SetClockTo(request, clockOffset);
        var authorization = request.Fields.Single(field => field.Key == "Authorization").Value;
        var method = new AwsSigV4AuthenticationMethod(accounts ?? Accounts, clock);

        return await method.StartConnection()
            .VerifyAsync(authorization[(authorization.IndexOf(' ', StringComparison.Ordinal) + 1)..], request, CancellationToken.None);
    }

    private static void AssertAccepted(HttpCredentialCheck check)
    {
        Assert.AreEqual(HttpCredentialOutcome.Accepted, check.Outcome);
        Assert.AreEqual(KeyId, check.AccountName);
        Assert.AreEqual(KeyId, check.UserAsSent);
        Assert.AreEqual(0, check.WwwAuthenticateValues.Count);
    }

    private static void AssertRefused(HttpCredentialCheck check, string? userAsSent = KeyId)
    {
        Assert.AreEqual(HttpCredentialOutcome.Refused, check.Outcome);
        Assert.IsNull(check.AccountName);
        Assert.AreEqual(userAsSent, check.UserAsSent);
    }

    [TestMethod]
    [DataRow("aws-sigv4-get", DisplayName = "GET, s3")]
    [DataRow("aws-sigv4-unsigned-payload", DisplayName = "PUT -d body, s3, -H x-amz-content-sha256: UNSIGNED-PAYLOAD: the body is not bound")]
    [DataRow("aws-sigv4-upload", DisplayName = "-T, s3: curl sends UNSIGNED-PAYLOAD itself")]
    [DataRow("aws-sigv4-query", DisplayName = "query string, s3")]
    [DataRow("aws-sigv4-query-encoding", DisplayName = "query re-encoded and sorted")]
    [DataRow("aws-sigv4-path-encoding", DisplayName = "path encoded again for ec2")]
    [DataRow("aws-sigv4-provider-only", DisplayName = "--aws-sigv4 aws: x-aws-date, region and service from the host")]
    [DataRow("aws-sigv4-other-provider", DisplayName = "osc:osc: OSC4-HMAC-SHA256")]
    [DataRow("aws-sigv4-extra-headers", DisplayName = "-H fields signed, inner spaces made one")]
    public async Task RecordedSignature_AtTheRecordedTime_IsAcceptedAsTheKeysAccount(string caseName)
    {
        AssertAccepted(await VerifyAsync(RecordedFixture.ReadRequest(caseName)));
    }

    [TestMethod]
    public async Task RecordedRequestSendingAHeaderThreeTimes_SignsItsNameOnceAndIsAccepted()
    {
        // curl -H 'X-Test: b' -H 'x-test:  a  ' -H 'X-TEST: c'.
        var request = RecordedFixture.ReadRequest("aws-sigv4-repeated-header");

        Assert.AreEqual(3, request.Fields.Count(field => string.Equals(field.Key, "x-test", StringComparison.OrdinalIgnoreCase)));
        StringAssert.Contains(
            RecordedFixture.ReadAuthorization("aws-sigv4-repeated-header"),
            "SignedHeaders=host;x-amz-content-sha256;x-amz-date;x-test,",
            StringComparison.Ordinal);
        AssertAccepted(await VerifyAsync(request));
    }

    [TestMethod]
    [DataRow("host;host;x-amz-content-sha256;x-amz-date", DisplayName = "in the same case")]
    [DataRow("host;x-amz-content-sha256;x-amz-date;X-Amz-Date", DisplayName = "in another case")]
    public async Task SignedHeadersRepeatingAName_IsRefusedAsMalformedCredentials(string signedHeaders)
    {
        var check = await VerifyAsync(WithAuthorization(Credentials(signedHeaders: signedHeaders)));

        AssertRefused(check, userAsSent: null);
        Assert.AreEqual(HttpAuthenticationRefusal.MalformedCredentials, check.Refusal);
        Assert.IsNull(check.SignatureMismatch);
    }

    private static HttpAuthenticationRequest GetFixture => RecordedFixture.ReadRequest("aws-sigv4-get");

    private static HttpAuthenticationRequest WithField(HttpAuthenticationRequest request, string name, string? value) =>
        request with
        {
            Fields = [.. request.Fields
                .Where(field => !string.Equals(field.Key, name, StringComparison.OrdinalIgnoreCase))
                .Concat(value is null ? [] : [new KeyValuePair<string, string>(name, value)])],
        };

    private static HttpAuthenticationRequest WithAuthorization(string credentials) =>
        WithField(GetFixture, "Authorization", "AWS4-HMAC-SHA256 " + credentials);

    private static string SignatureOf(HttpAuthenticationRequest request)
    {
        var authorization = request.Fields.Single(field => field.Key == "Authorization").Value;

        return authorization[(authorization.IndexOf("Signature=", StringComparison.Ordinal) + "Signature=".Length)..];
    }

    private static string Credentials(
        string credential = KeyId + "/20260929/us-east-1/s3/aws4_request",
        string signedHeaders = "host;x-amz-content-sha256;x-amz-date",
        string? signature = null) =>
        $"Credential={credential}, SignedHeaders={signedHeaders}, Signature={signature ?? SignatureOf(GetFixture)}";

    [TestMethod]
    public async Task RecordedSignature_AbsoluteFormTarget_IsAccepted()
    {
        AssertAccepted(await VerifyAsync(GetFixture with { Target = "http://127.0.0.1:18122/x" }));
    }

    [TestMethod]
    public async Task RecordedSignature_AnUnsignedFieldAdded_IsAccepted()
    {
        AssertAccepted(await VerifyAsync(WithField(GetFixture, "X-Unsigned", "anything")));
    }

    [TestMethod]
    public async Task RecordedSignature_ExplicitEmptyContentLength_IsAccepted()
    {
        // path-encoding (ec2) sent no x-amz-content-sha256, so its payload hash is the empty body's.
        AssertAccepted(await VerifyAsync(WithField(RecordedFixture.ReadRequest("aws-sigv4-path-encoding"), "Content-Length", "0")));
    }

    [TestMethod]
    [DataRow(-15, DisplayName = "15 minutes after the request")]
    [DataRow(15, DisplayName = "15 minutes before the request")]
    public async Task RecordedSignature_AtTheEdgeOfTheWindow_IsAccepted(int minutes)
    {
        AssertAccepted(await VerifyAsync(GetFixture, clockOffset: TimeSpan.FromMinutes(-minutes)));
    }

    [TestMethod]
    [DataRow(1, DisplayName = "server clock 15 min 1 s ahead")]
    [DataRow(-1, DisplayName = "server clock 15 min 1 s behind")]
    public async Task RecordedSignature_OutsideTheWindow_IsRefused(int direction)
    {
        var offset = direction * (AwsSigV4AuthenticationMethod.RequestTimeWindow + TimeSpan.FromSeconds(1));

        AssertRefused(await VerifyAsync(GetFixture, clockOffset: offset));
    }

    [TestMethod]
    public async Task RecordedSignature_ChangedMethod_IsRefused()
    {
        AssertRefused(await VerifyAsync(GetFixture with { Method = "HEAD" }));
    }

    [TestMethod]
    public async Task RecordedSignature_ChangedPath_IsRefused()
    {
        AssertRefused(await VerifyAsync(GetFixture with { Target = "/y" }));
    }

    [TestMethod]
    public async Task RecordedSignature_ChangedQuery_IsRefused()
    {
        var request = RecordedFixture.ReadRequest("aws-sigv4-query");

        AssertRefused(await VerifyAsync(request with { Target = request.Target.Replace("a=1", "a=2", StringComparison.Ordinal) }));
    }

    [TestMethod]
    public async Task RecordedSignature_ChangedSignedHeader_IsRefused()
    {
        AssertRefused(await VerifyAsync(WithField(GetFixture, "Host", "127.0.0.1:18123")));
    }

    [TestMethod]
    public async Task RecordedSignature_ChangedBodyHash_IsRefused()
    {
        var request = RecordedFixture.ReadRequest("aws-sigv4-put");

        AssertRefused(await VerifyAsync(WithField(request, "x-amz-content-sha256", new string('0', 64))));
    }

    [TestMethod]
    public async Task RecordedSignature_WrongSecret_IsRefused()
    {
        AssertRefused(await VerifyAsync(GetFixture, new AccountBook([new Account(KeyId, "secreT")])));
    }

    [TestMethod]
    public async Task RecordedWrongSecret_IsRefused()
    {
        // Signed by curl with -u AKIDEXAMPLE:wrong.
        AssertRefused(await VerifyAsync(RecordedFixture.ReadRequest("aws-sigv4-refused")));
    }

    [TestMethod]
    public async Task RecordedSignature_UnknownKey_IsRefusedAfterTheSameComparison()
    {
        var comparer = new CountingSecretComparer();

        AssertRefused(await VerifyAsync(GetFixture, new AccountBook([new Account("other", "secret")], comparer)));
        Assert.AreEqual((32, 32), comparer.Comparisons.Single());
    }

    [TestMethod]
    public async Task EmptyAccessKey_NeverMatchesTheBearerTokensAccount()
    {
        var request = WithAuthorization(Credentials(credential: "/20260929/us-east-1/s3/aws4_request"));

        AssertRefused(await VerifyAsync(request, new AccountBook([new Account(string.Empty, "secret")])), string.Empty);
    }

    // The body both -d body recordings sent (Fixtures/README.md).
    private static readonly byte[] RecordedBodySha256 = SHA256.HashData("body"u8);

    private static readonly byte[] ChangedBodySha256 = SHA256.HashData("bodY"u8);

    // The s3 PUT sends its content hash, so its signature is verified on the head and the check
    // names the account pending the body; the others wait for the body to verify it at all.
    private static HttpCredentialCheck AssertAwaitingBody(HttpCredentialCheck check, string? verifiedAccountName = null)
    {
        Assert.AreEqual(HttpCredentialOutcome.AwaitingBody, check.Outcome);
        Assert.AreEqual(verifiedAccountName, check.AccountName);
        Assert.AreEqual(KeyId, check.UserAsSent);
        Assert.IsNotNull(check.CheckBody);

        return check;
    }

    [TestMethod]
    [DataRow("aws-sigv4-put", DisplayName = "s3: the body must hash to x-amz-content-sha256")]
    [DataRow("aws-sigv4-ec2-put", DisplayName = "ec2: the signature is over the body's hash")]
    public async Task RecordedPut_WithTheSignedBody_IsAcceptedOnceTheBodyIsRead(string caseName)
    {
        var check = AssertAwaitingBody(await VerifyAsync(RecordedFixture.ReadRequest(caseName)), caseName == "aws-sigv4-put" ? KeyId : null);

        AssertAccepted(check.CheckBody!(RecordedBodySha256));
    }

    [TestMethod]
    [DataRow("aws-sigv4-put", DisplayName = "s3")]
    [DataRow("aws-sigv4-ec2-put", DisplayName = "ec2")]
    public async Task RecordedPut_WithAChangedBody_IsRefused(string caseName)
    {
        var check = AssertAwaitingBody(await VerifyAsync(RecordedFixture.ReadRequest(caseName)), caseName == "aws-sigv4-put" ? KeyId : null);

        AssertRefused(check.CheckBody!(ChangedBodySha256));
    }

    [TestMethod]
    public async Task RecordedEc2Upload_IsRefusedSinceCurlSignedTheEmptyBody()
    {
        // curl -T with a service other than s3 signs the empty body's hash and sends the file,
        // so the body received is not the one signed (ADR-0045); AWS refuses it the same way.
        var check = AssertAwaitingBody(await VerifyAsync(RecordedFixture.ReadRequest("aws-sigv4-ec2-upload")));

        AssertRefused(check.CheckBody!(RecordedBodySha256));
        AssertAccepted(check.CheckBody!(SHA256.HashData([])));
    }

    [TestMethod]
    public async Task RecordedPut_WrongSecret_IsRefusedBeforeTheBody()
    {
        // With the hash sent, the signature is checked on the head, so the body is never read.
        AssertRefused(await VerifyAsync(RecordedFixture.ReadRequest("aws-sigv4-put"), new AccountBook([new Account(KeyId, "secreT")])));
    }

    [TestMethod]
    public async Task RecordedEc2Put_WrongSecret_IsRefusedOnceTheBodyIsRead()
    {
        var check = AssertAwaitingBody(await VerifyAsync(
            RecordedFixture.ReadRequest("aws-sigv4-ec2-put"), new AccountBook([new Account(KeyId, "secreT")])));

        AssertRefused(check.CheckBody!(RecordedBodySha256));
    }

    [TestMethod]
    public async Task RecordedEc2Put_UnknownKey_IsRefusedAfterTheSameComparison()
    {
        var comparer = new CountingSecretComparer();
        var check = AssertAwaitingBody(await VerifyAsync(
            RecordedFixture.ReadRequest("aws-sigv4-ec2-put"), new AccountBook([new Account("other", "secret")], comparer)));

        AssertRefused(check.CheckBody!(RecordedBodySha256));
        Assert.AreEqual((32, 32), comparer.Comparisons.Single());
    }

    [TestMethod]
    [DataRow("aws-sigv4-put", DisplayName = "s3")]
    [DataRow("aws-sigv4-ec2-put", DisplayName = "ec2")]
    public async Task RecordedPut_OutsideTheWindow_IsRefusedBeforeTheBody(string caseName)
    {
        var offset = AwsSigV4AuthenticationMethod.RequestTimeWindow + TimeSpan.FromSeconds(1);

        AssertRefused(await VerifyAsync(RecordedFixture.ReadRequest(caseName), clockOffset: offset));
    }

    [TestMethod]
    public async Task RecordedEc2Put_SignedHeaderMissing_IsRefusedBeforeTheBody()
    {
        // Host is signed; without it the canonical request cannot be built, whatever the body.
        AssertRefused(await VerifyAsync(WithField(RecordedFixture.ReadRequest("aws-sigv4-ec2-put"), "Host", null)));
    }

    [TestMethod]
    public async Task RecordedPut_BodyRemoved_IsRefusedSinceTheEmptyBodyIsNotTheOneSigned()
    {
        // Content-Length is not signed, so the signature still verifies; the sent hash is "body"'s.
        var request = RecordedFixture.ReadRequest("aws-sigv4-put");

        AssertRefused(await VerifyAsync(WithField(request, "Content-Length", null)));
    }

    [TestMethod]
    public async Task ChunkedRequestWithNoContentHash_AwaitsTheBody()
    {
        var request = RecordedFixture.ReadRequest("aws-sigv4-path-encoding");

        var check = AssertAwaitingBody(await VerifyAsync(WithField(request, "Transfer-Encoding", "chunked")));

        // Signed over the empty body, so only the empty body's hash completes it.
        AssertRefused(check.CheckBody!(RecordedBodySha256));
        AssertAccepted(check.CheckBody!(SHA256.HashData([])));
    }

    [TestMethod]
    [DataRow("host;x-amz-content-sha256", DisplayName = "date not signed")]
    [DataRow("x-amz-content-sha256;x-amz-date", DisplayName = "host not signed")]
    [DataRow("host;x--date;x-amz-date", DisplayName = "empty provider in a date name is not the date")]
    [DataRow("host;x-amz-content-sha256;x-amz-date;x-missing", DisplayName = "signed field not in the request")]
    [DataRow("host;x-amz-content-sha256;x-osc-date", DisplayName = "signed date field not in the request")]
    public async Task SignedHeaders_WithoutWhatMustBeSigned_AreRefused(string signedHeaders)
    {
        AssertRefused(await VerifyAsync(WithAuthorization(Credentials(signedHeaders: signedHeaders))));
    }

    [TestMethod]
    public async Task ScopeDateNotTheRequestsDate_IsRefused()
    {
        AssertRefused(await VerifyAsync(WithAuthorization(Credentials(credential: KeyId + "/20260930/us-east-1/s3/aws4_request"))));
    }

    [TestMethod]
    public async Task UnreadableDateField_IsRefused()
    {
        SetClockTo(GetFixture);
        var method = new AwsSigV4AuthenticationMethod(Accounts, clock);

        var check = await method.VerifyAsync(
            Credentials(), WithField(GetFixture, "X-Amz-Date", "yesterday"), CancellationToken.None);

        AssertRefused(check);
    }

    [TestMethod]
    [DataRow("*", DisplayName = "asterisk form")]
    [DataRow("x/y", DisplayName = "no leading slash")]
    [DataRow("http://127.0.0.1:18122", DisplayName = "absolute form with no path")]
    public async Task TargetWithNoPath_IsRefused(string target)
    {
        AssertRefused(await VerifyAsync(GetFixture with { Target = target }));
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("Credential=a/b/c/d/aws4_request, SignedHeaders=host", DisplayName = "no Signature")]
    [DataRow("Credential=x, Credential=y, Signature=z", DisplayName = "a parameter twice")]
    [DataRow("Credential=x, SignedHeaders=y, Signature=z, Extra=1", DisplayName = "a fourth parameter")]
    [DataRow("Credential=x, SignedHeaders=y, Signatures=z", DisplayName = "a misspelt parameter")]
    [DataRow("Credential, SignedHeaders=y, Signature=z", DisplayName = "a parameter with no =")]
    public async Task MalformedCredentials_AreRefusedWithNoUser(string credentials)
    {
        AssertRefused(await VerifyAsync(WithAuthorization(credentials)), null);
    }

    [TestMethod]
    [DataRow("AKIDEXAMPLE/20260929/us-east-1/aws4_request", "host", null, DisplayName = "scope of three parts")]
    [DataRow("AKIDEXAMPLE/20260929/us-east-1/s3/aws4_requests", "host", null, DisplayName = "terminator not 4_request")]
    [DataRow("AKIDEXAMPLE/20260929/us-east-1/s3/AWS4_request", "host", null, DisplayName = "terminator in upper case")]
    [DataRow("AKIDEXAMPLE/20260929/us-east-1/s3/4_request", "host", null, DisplayName = "terminator with no provider")]
    [DataRow("AKIDEXAMPLE/20260929/us-east-1/s3/aws4_request", "", null, DisplayName = "no signed headers")]
    [DataRow("AKIDEXAMPLE/20260929/us-east-1/s3/aws4_request", "host", "abc", DisplayName = "short signature")]
    [DataRow("AKIDEXAMPLE/20260929/us-east-1/s3/aws4_request", "host", "B09D624160CF3E4EB31422DE27D9396E3F9B96A7C7E51024BF516FE1323E0D9B", DisplayName = "signature in upper case")]
    public async Task MalformedCredentialParts_AreRefusedWithNoUser(string credential, string signedHeaders, string? signature)
    {
        AssertRefused(await VerifyAsync(WithAuthorization(Credentials(credential, signedHeaders, signature))), null);
    }

    [TestMethod]
    public async Task AccessKeyHoldingASlash_IsReadWhole()
    {
        var request = WithAuthorization(Credentials(credential: "AKID/EXAMPLE/20260929/us-east-1/s3/aws4_request"));

        AssertRefused(await VerifyAsync(request), "AKID/EXAMPLE");
    }

    [TestMethod]
    public void Method_IsAwsSigV4_WithNoChallengeAndOneSharedVerifier()
    {
        var method = new AwsSigV4AuthenticationMethod(Accounts, clock);

        Assert.AreEqual(AuthenticationMethod.AwsSigV4, method.Method);
        Assert.IsEmpty(method.CreateChallenges());
        Assert.AreSame(method, method.StartConnection());
    }

    [TestMethod]
    public void Constructor_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new AwsSigV4AuthenticationMethod(null!, clock));
        Assert.ThrowsExactly<ArgumentNullException>(() => new AwsSigV4AuthenticationMethod(Accounts, null!));
    }

    [TestMethod]
    public async Task VerifyAsync_NullArguments_Throw()
    {
        var method = new AwsSigV4AuthenticationMethod(Accounts, clock);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => method.VerifyAsync(null!, GetFixture, CancellationToken.None).AsTask());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => method.VerifyAsync(string.Empty, null!, CancellationToken.None).AsTask());
    }

    [TestMethod]
    public async Task VerifyAsync_Cancelled_Throws()
    {
        var method = new AwsSigV4AuthenticationMethod(Accounts, clock);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => method.VerifyAsync(string.Empty, GetFixture, new CancellationToken(canceled: true)).AsTask());
    }

    private AuthenticationPolicy CreatePolicy(IReadOnlySet<AuthenticationMethod>? acceptedMethods = null) =>
        PolicyFixture.Create(
            Accounts,
            clock,
            acceptedMethods: acceptedMethods,
            httpMethods: [new AwsSigV4AuthenticationMethod(Accounts, clock), new BearerAuthenticationMethod(Accounts)]);

    [TestMethod]
    public async Task Policy_RecordedSignature_ProceedsWithTheLoginNote()
    {
        SetClockTo(GetFixture);

        var verdict = await CreatePolicy().StartHttpConnection(null).JudgeAsync(GetFixture, CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.AreEqual(KeyId, verdict.AccountName);
        Assert.AreEqual("Login accepted: AWS4-HMAC-SHA256 AKIDEXAMPLE", verdict.CheckedLogin?.Note);
    }

    [TestMethod]
    [DataRow("aws-sigv4-put", DisplayName = "s3")]
    [DataRow("aws-sigv4-ec2-put", DisplayName = "ec2")]
    public async Task Policy_RecordedPutWithItsBody_IsServedWithTheLoginNote(string caseName)
    {
        var request = RecordedFixture.ReadRequest(caseName);
        SetClockTo(request);
        var verdict = await CreatePolicy().StartHttpConnection(null).JudgeAsync(request, CancellationToken.None);

        var bodyVerdict = await verdict.BodyCheck!.JudgeBodyAsync(RecordedBodySha256, CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.IsNull(verdict.CheckedLogin);
        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, bodyVerdict.Outcome);
        Assert.AreEqual(KeyId, bodyVerdict.AccountName);
        Assert.AreEqual("Login accepted: AWS4-HMAC-SHA256 AKIDEXAMPLE", bodyVerdict.CheckedLogin?.Note);
    }

    [TestMethod]
    [DataRow("aws-sigv4-put", DisplayName = "s3")]
    [DataRow("aws-sigv4-ec2-put", DisplayName = "ec2")]
    public async Task Policy_RecordedPutWithAChangedBody_IsChallengedAfterTheRefusalDelay(string caseName)
    {
        var request = RecordedFixture.ReadRequest(caseName);
        SetClockTo(request);
        var verdict = await CreatePolicy().StartHttpConnection(PolicyFixture.Tls).JudgeAsync(request, CancellationToken.None);

        var judgement = verdict.BodyCheck!.JudgeBodyAsync(ChangedBodySha256, CancellationToken.None).AsTask();
        Assert.IsFalse(judgement.IsCompleted);
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var bodyVerdict = await judgement;

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, bodyVerdict.Outcome);
        CollectionAssert.AreEqual(new[] { BearerAuthenticationMethod.Challenge }, bodyVerdict.WwwAuthenticateValues.ToArray());
        Assert.AreEqual("Login refused: AWS4-HMAC-SHA256 AKIDEXAMPLE", bodyVerdict.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task Policy_OtherProvidersScheme_IsCheckedAsSignatureVersion4()
    {
        var request = RecordedFixture.ReadRequest("aws-sigv4-other-provider");
        SetClockTo(request);

        var verdict = await CreatePolicy().StartHttpConnection(null).JudgeAsync(request, CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.AreEqual(KeyId, verdict.AccountName);
    }

    [TestMethod]
    public async Task Policy_WrongSecret_IsChallengedAfterTheRefusalDelayWithTheOtherChallenges()
    {
        var request = RecordedFixture.ReadRequest("aws-sigv4-refused");
        SetClockTo(request);
        var judgement = CreatePolicy().StartHttpConnection(PolicyFixture.Tls).JudgeAsync(request, CancellationToken.None).AsTask();
        Assert.IsFalse(judgement.IsCompleted);

        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var verdict = await judgement;

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        CollectionAssert.AreEqual(new[] { BearerAuthenticationMethod.Challenge }, verdict.WwwAuthenticateValues.ToArray());
        Assert.AreEqual("Login refused: AWS4-HMAC-SHA256 AKIDEXAMPLE", verdict.CheckedLogin?.Note);
    }

    // AWS's documented SigV4 "GET Object" example (Amazon S3 API Reference, "Signature
    // Calculations for the Authorization Header: Transferring Payload in a Single Chunk").
    private const string ExampleAccessKeyId = "AKIAIOSFODNN7EXAMPLE";

    private const string ExampleSecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    private const string ExampleSignature = "f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41";

    private static readonly string ExampleCanonicalRequest = string.Join(
        '\n',
        "GET",
        "/test.txt",
        string.Empty,
        "host:examplebucket.s3.amazonaws.com",
        "range:bytes=0-9",
        "x-amz-content-sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "x-amz-date:20130524T000000Z",
        string.Empty,
        "host;range;x-amz-content-sha256;x-amz-date",
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

    private static readonly string ExampleStringToSign = string.Join(
        '\n',
        "AWS4-HMAC-SHA256",
        "20130524T000000Z",
        "20130524/us-east-1/s3/aws4_request",
        "7344ae5b7ee6c3e7e6b0fe0640412a37625d1fbfff95c48bbb2dc43964946972");

    private static HttpAuthenticationRequest ExampleGetObject(
        string signature, string? service = "s3", string accessKeyId = ExampleAccessKeyId) =>
        new(
            "GET",
            "/test.txt",
            false,
            [
                new("Host", "examplebucket.s3.amazonaws.com"),
                new(
                    "Authorization",
                    $"AWS4-HMAC-SHA256 Credential={accessKeyId}/20130524/us-east-1/s3/aws4_request,"
                    + $"SignedHeaders=host;range;x-amz-content-sha256;x-amz-date,Signature={signature}"),
                new("Range", "bytes=0-9"),
                new("x-amz-content-sha256", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"),
                new("x-amz-date", "20130524T000000Z"),
            ],
            service);

    // The signature a client holding the wrong secret sends over the example's string to sign.
    private static string SignatureUnderAWrongSecret()
    {
        byte[] key = System.Text.Encoding.UTF8.GetBytes("AWS4wrong");
        foreach (var part in new[] { "20130524", "us-east-1", "s3", "aws4_request" })
        {
            key = HMACSHA256.HashData(key, System.Text.Encoding.UTF8.GetBytes(part));
        }

        return Convert.ToHexStringLower(HMACSHA256.HashData(key, System.Text.Encoding.UTF8.GetBytes(ExampleStringToSign)));
    }

    // Judged by a policy holding the example's account, letting the refusal delay pass.
    private async Task<HttpAuthenticationVerdict> JudgeExampleAsync(HttpAuthenticationRequest request)
    {
        var accounts = new AccountBook([new Account(ExampleAccessKeyId, ExampleSecretAccessKey)]);
        clock.Advance(new DateTimeOffset(2013, 5, 24, 0, 0, 0, TimeSpan.Zero) - clock.GetUtcNow());
        var policy = PolicyFixture.Create(accounts, clock, httpMethods: [new AwsSigV4AuthenticationMethod(accounts, clock)]);

        var judgement = policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(request, CancellationToken.None).AsTask();
        if (!judgement.IsCompleted)
        {
            clock.Advance(AuthenticationPolicy.RefusalDelay);
        }

        return await judgement;
    }

    [TestMethod]
    [DataRow("s3", DisplayName = "the S3 server's request")]
    [DataRow(null, DisplayName = "an HTTP request with no other method to challenge with")]
    public async Task Policy_ExampleGetObjectSignedWithAWrongSecret_CarriesTheCanonicalRequestAndStringToSign(string? service)
    {
        var verdict = await JudgeExampleAsync(ExampleGetObject(SignatureUnderAWrongSecret(), service));

        Assert.AreEqual(HttpAuthenticationOutcome.Forbidden, verdict.Outcome);
        Assert.AreEqual(HttpAuthenticationRefusal.BadSignature, verdict.Refusal);
        Assert.AreEqual(ExampleCanonicalRequest, verdict.SignatureMismatch?.CanonicalRequest);
        Assert.AreEqual(ExampleStringToSign, verdict.SignatureMismatch?.StringToSign);
    }

    [TestMethod]
    public async Task Policy_HttpRequestChallengedAsABadSignature_CarriesTheMismatch()
    {
        var request = RecordedFixture.ReadRequest("aws-sigv4-refused");
        SetClockTo(request);
        var judgement = CreatePolicy().StartHttpConnection(PolicyFixture.Tls).JudgeAsync(request, CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);

        var verdict = await judgement;

        Assert.AreEqual(HttpAuthenticationOutcome.Challenge, verdict.Outcome);
        Assert.AreEqual(HttpAuthenticationRefusal.BadSignature, verdict.Refusal);
        Assert.IsNotNull(verdict.SignatureMismatch);
    }

    [TestMethod]
    public async Task Policy_ExampleGetObjectRightlySigned_IsAcceptedWithNoMismatch()
    {
        var verdict = await JudgeExampleAsync(ExampleGetObject(ExampleSignature));

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.IsNull(verdict.SignatureMismatch);
    }

    [TestMethod]
    public async Task Policy_UnknownAccessKey_HasNoMismatch()
    {
        var verdict = await JudgeExampleAsync(ExampleGetObject(ExampleSignature, accessKeyId: "AKIAUNKNOWNEXAMPLE00"));

        Assert.AreEqual(HttpAuthenticationRefusal.UnknownAccount, verdict.Refusal);
        Assert.IsNull(verdict.SignatureMismatch);
    }

    [TestMethod]
    [DataRow("Range", null, HttpAuthenticationRefusal.BadSignature, DisplayName = "a bad signature with no canonical request to build")]
    [DataRow("x-amz-date", "20130524T010000Z", HttpAuthenticationRefusal.ClockSkew, DisplayName = "clock skew")]
    [DataRow("x-amz-content-sha256", null, HttpAuthenticationRefusal.MissingPayloadHash, DisplayName = "no content hash")]
    public async Task Policy_ARefusalWithNothingCompared_HasNoMismatch(string name, string? value, HttpAuthenticationRefusal refusal)
    {
        var verdict = await JudgeExampleAsync(WithField(ExampleGetObject(ExampleSignature), name, value));

        Assert.AreEqual(refusal, verdict.Refusal);
        Assert.IsNull(verdict.SignatureMismatch);
    }

    [TestMethod]
    public async Task Policy_RefuseLoginFaultOnARightSignature_IsABadSignatureWithNoMismatch()
    {
        var accounts = new AccountBook([new Account(ExampleAccessKeyId, ExampleSecretAccessKey)]);
        clock.Advance(new DateTimeOffset(2013, 5, 24, 0, 0, 0, TimeSpan.Zero) - clock.GetUtcNow());
        var policy = PolicyFixture.CreateObserved(
            accounts,
            clock,
            new RecordingSurlEventPublisher(),
            new SpendingFaultPlan(new FaultRule("refuse", FaultKind.RefuseLogin)),
            httpMethods: [new AwsSigV4AuthenticationMethod(accounts, clock)]);

        var judgement = policy.StartHttpConnection(null).JudgeAsync(ExampleGetObject(ExampleSignature), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var verdict = await judgement;

        Assert.AreEqual(HttpAuthenticationRefusal.BadSignature, verdict.Refusal);
        Assert.IsNull(verdict.SignatureMismatch);
    }

    [TestMethod]
    public async Task Policy_SignedSecurityToken_IsWithheldFromTheCanonicalRequestButNotFromItsHash()
    {
        var request = TokenRequest(
            "/test.txt?X-Amz-Security-Token=query%2Ftoken&a=1",
            ("host", "examplebucket.s3.amazonaws.com"),
            ("x-amz-content-sha256", "UNSIGNED-PAYLOAD"),
            ("x-amz-date", "20130524T000000Z"),
            ("x-amz-security-token", "header-token"));

        var verdict = await JudgeExampleAsync(request);

        var real = string.Join(
            '\n',
            "GET",
            "/test.txt",
            "X-Amz-Security-Token=query%2Ftoken&a=1",
            "host:examplebucket.s3.amazonaws.com",
            "x-amz-content-sha256:UNSIGNED-PAYLOAD",
            "x-amz-date:20130524T000000Z",
            "x-amz-security-token:header-token",
            string.Empty,
            "host;x-amz-content-sha256;x-amz-date;x-amz-security-token",
            "UNSIGNED-PAYLOAD");
        Assert.AreEqual(
            real.Replace("query%2Ftoken", "****", StringComparison.Ordinal).Replace("header-token", "****", StringComparison.Ordinal),
            verdict.SignatureMismatch?.CanonicalRequest);
        StringAssert.Contains(verdict.SignatureMismatch!.CanonicalRequest, "\nx-amz-security-token:****\n");
        Assert.AreEqual(
            Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(real))),
            verdict.SignatureMismatch.StringToSign.Split('\n')[^1]);
    }

    // Each name of the shared list as it reads there and in lower case, as a client signs it.
    private static IEnumerable<object[]> WithheldHeaderNames =>
        HttpWithheldHeaderNames.All
            .SelectMany(name => new[] { name, name.ToLowerInvariant() })
            .Select(name => new object[] { name });

    [TestMethod]
    [DynamicData(nameof(WithheldHeaderNames))]
    public async Task Policy_ASignedHeaderTheExchangeLogWithholds_IsWithheldFromTheCanonicalRequest(string name)
    {
        var request = TokenRequest(
            "/test.txt",
            ("host", "examplebucket.s3.amazonaws.com"),
            (name, "kept-from-the-log"),
            ("x-amz-content-sha256", "UNSIGNED-PAYLOAD"),
            ("x-amz-date", "20130524T000000Z"));

        var verdict = await JudgeExampleAsync(request);

        var canonicalRequest = verdict.SignatureMismatch!.CanonicalRequest;
        StringAssert.Contains(canonicalRequest, $"\n{name}:****\n");
        Assert.IsFalse(canonicalRequest.Contains("kept-from-the-log", StringComparison.Ordinal));
        Assert.IsFalse(canonicalRequest.Contains(new string('0', 64), StringComparison.Ordinal));
    }

    // A GET signing every field given, in order, with a signature that does not match.
    private static HttpAuthenticationRequest TokenRequest(string target, params (string Name, string Value)[] fields)
    {
        var signedHeaders = string.Join(';', fields.Select(field => field.Name));
        var authorization = $"AWS4-HMAC-SHA256 Credential={ExampleAccessKeyId}/20130524/us-east-1/s3/aws4_request,"
            + $"SignedHeaders={signedHeaders},Signature={new string('0', 64)}";
        var sent = fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value));

        // The credentials come first, so a signed field that is itself an Authorization is not read as them.
        return new("GET", target, false, [new("Authorization", authorization), .. sent], "s3");
    }

    [TestMethod]
    public async Task Policy_OnlySignatureVersion4Accepted_NoCredentials_IsForbidden()
    {
        var policy = CreatePolicy(new HashSet<AuthenticationMethod> { AuthenticationMethod.AwsSigV4 });

        var verdict = await policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Forbidden, verdict.Outcome);
    }
}
