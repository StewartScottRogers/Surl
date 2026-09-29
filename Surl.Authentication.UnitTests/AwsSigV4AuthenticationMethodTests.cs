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

    private static HttpCredentialCheck AssertAwaitingBody(HttpCredentialCheck check)
    {
        Assert.AreEqual(HttpCredentialOutcome.AwaitingBody, check.Outcome);
        Assert.IsNull(check.AccountName);
        Assert.AreEqual(KeyId, check.UserAsSent);
        Assert.IsNotNull(check.CheckBody);

        return check;
    }

    [TestMethod]
    [DataRow("aws-sigv4-put", DisplayName = "s3: the body must hash to x-amz-content-sha256")]
    [DataRow("aws-sigv4-ec2-put", DisplayName = "ec2: the signature is over the body's hash")]
    public async Task RecordedPut_WithTheSignedBody_IsAcceptedOnceTheBodyIsRead(string caseName)
    {
        var check = AssertAwaitingBody(await VerifyAsync(RecordedFixture.ReadRequest(caseName)));

        AssertAccepted(check.CheckBody!(RecordedBodySha256));
    }

    [TestMethod]
    [DataRow("aws-sigv4-put", DisplayName = "s3")]
    [DataRow("aws-sigv4-ec2-put", DisplayName = "ec2")]
    public async Task RecordedPut_WithAChangedBody_IsRefused(string caseName)
    {
        var check = AssertAwaitingBody(await VerifyAsync(RecordedFixture.ReadRequest(caseName)));

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

    [TestMethod]
    public async Task Policy_OnlySignatureVersion4Accepted_NoCredentials_IsForbidden()
    {
        var policy = CreatePolicy(new HashSet<AuthenticationMethod> { AuthenticationMethod.AwsSigV4 });

        var verdict = await policy.StartHttpConnection(null).JudgeAsync(PolicyFixture.Get(), CancellationToken.None);

        Assert.AreEqual(HttpAuthenticationOutcome.Forbidden, verdict.Outcome);
    }
}
