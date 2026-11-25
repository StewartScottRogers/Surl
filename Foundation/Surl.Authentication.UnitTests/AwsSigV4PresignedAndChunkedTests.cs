using System.Security.Cryptography;
using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// The S3 server's presigned URLs and <c>aws-chunked</c> bodies (ADR-0108 decision 4.5), judged as
/// a request naming its <see cref="HttpAuthenticationRequest.AwsSigV4Service"/>. The requests are
/// the Amazon S3 API Reference's worked examples, with their access key, secret and signatures:
/// "Authenticating Requests: Using Query Parameters (AWS Signature Version 4)" (a GET of
/// <c>test.txt</c> from <c>examplebucket</c>, valid 86400 seconds), and "Signature Calculations
/// for the Authorization Header: Transferring Payload in Multiple Chunks (Chunked Upload)" (a PUT
/// of 66560 bytes of <c>a</c> in chunks of 65536 and 1024 bytes, then the final empty chunk).
/// </summary>
[TestClass]
public sealed class AwsSigV4PresignedAndChunkedTests
{
    private const string ExampleAccessKeyId = "AKIAIOSFODNN7EXAMPLE";

    private const string ExampleSecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY";

    private const string PresignedSignature = "aeeed9bbccd4d02ee5c0109b86d86835f995330da4c265957d157751f604d404";

    private const string SeedSignature = "4f232c4386841ef735655705268965c44a0e4690baa4adea153f7db9fa80a0a9";

    private const string FirstChunkSignature = "ad80c730a21e5b8d04586a2213dd63b9a0e99e0e2307b0ade35a65485a288648";

    private const string SecondChunkSignature = "0055627c9e194cb4542bae2aa5492e3c1575bbb81b612b7d234b86a503ef5497";

    private const string FinalChunkSignature = "b6c6ea8a5354eaf15b3cb7646744f4275b71ea724fed81ceb9323e279d449df9";

    // Both examples' date, 20130524T000000Z.
    private static readonly DateTimeOffset ExampleDate = new(2013, 5, 24, 0, 0, 0, TimeSpan.Zero);

    private static readonly AccountBook Accounts = new([new Account(ExampleAccessKeyId, ExampleSecretAccessKey)]);

    private readonly ManualTimeProvider clock = new();

    private static string PresignedTarget(
        string credential = "AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request",
        string algorithm = "AWS4-HMAC-SHA256",
        string date = "20130524T000000Z",
        string expires = "86400",
        string signedHeaders = "host",
        string signature = PresignedSignature) =>
        $"/test.txt?X-Amz-Algorithm={algorithm}&X-Amz-Credential={credential}&X-Amz-Date={date}"
        + $"&X-Amz-Expires={expires}&X-Amz-SignedHeaders={signedHeaders}&X-Amz-Signature={signature}";

    private static HttpAuthenticationRequest PresignedGet(string target, params KeyValuePair<string, string>[] extraFields) =>
        new("GET", target, false, [new("Host", "examplebucket.s3.amazonaws.com"), .. extraFields], "s3");

    private static string ChunkedAuthorization(string signature = SeedSignature) =>
        "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,"
        + "SignedHeaders=content-encoding;content-length;host;x-amz-content-sha256;x-amz-date;x-amz-decoded-content-length;x-amz-storage-class,"
        + $"Signature={signature}";

    private static HttpAuthenticationRequest ChunkedPut(string authorization, string contentSha256 = SignatureV4Check.StreamingSignedPayload, string service = "s3") =>
        new(
            "PUT",
            "/examplebucket/chunkObject.txt",
            true,
            [
                new("Host", "s3.amazonaws.com"),
                new("x-amz-date", "20130524T000000Z"),
                new("x-amz-storage-class", "REDUCED_REDUNDANCY"),
                new("Authorization", authorization),
                new("x-amz-content-sha256", contentSha256),
                new("Content-Encoding", "aws-chunked"),
                new("x-amz-decoded-content-length", "66560"),
                new("Content-Length", "66824"),
            ],
            service);

    private AuthenticationPolicy CreatePolicy(TimeSpan clockOffset = default)
    {
        clock.Advance(ExampleDate + clockOffset - clock.GetUtcNow());

        return PolicyFixture.Create(
            Accounts,
            clock,
            false,
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
        Assert.AreEqual(refusal == HttpAuthenticationRefusal.BadSignature, verdict.SignatureMismatch is not null);
    }

    private static byte[] Sha256OfAs(int count) => SHA256.HashData(Enumerable.Repeat((byte)'a', count).ToArray());

    [TestMethod]
    public async Task PresignedWorkedExample_AtItsDate_IsAcceptedAsTheKeysAccount()
    {
        var verdict = await JudgeAsync(CreatePolicy(), PresignedGet(PresignedTarget()));

        Assert.AreEqual(HttpAuthenticationOutcome.Proceed, verdict.Outcome);
        Assert.AreEqual(ExampleAccessKeyId, verdict.AccountName);
        Assert.IsNull(verdict.BodyCheck);
        Assert.AreEqual("Login accepted: AWS4-HMAC-SHA256 AKIAIOSFODNN7EXAMPLE", verdict.CheckedLogin?.Note);
    }

    [TestMethod]
    [DataRow(86400, DisplayName = "at the last second")]
    [DataRow(-15 * 60, DisplayName = "15 minutes before its date")]
    public async Task PresignedWorkedExample_InsideItsValidity_IsAccepted(int secondsAfter)
    {
        var verdict = await JudgeAsync(CreatePolicy(TimeSpan.FromSeconds(secondsAfter)), PresignedGet(PresignedTarget()));

        Assert.AreEqual(ExampleAccessKeyId, verdict.AccountName);
    }

    [TestMethod]
    public async Task PresignedWorkedExample_ASecondPastItsExpiry_IsForbiddenAsExpired()
    {
        var verdict = await JudgeAsync(CreatePolicy(TimeSpan.FromSeconds(86401)), PresignedGet(PresignedTarget()));

        AssertForbidden(verdict, HttpAuthenticationRefusal.SignatureExpired);
    }

    [TestMethod]
    public async Task PresignedWorkedExample_SixteenMinutesBeforeItsDate_IsForbiddenAsClockSkew()
    {
        var verdict = await JudgeAsync(CreatePolicy(TimeSpan.FromMinutes(-16)), PresignedGet(PresignedTarget()));

        AssertForbidden(verdict, HttpAuthenticationRefusal.ClockSkew);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("604801")]
    [DataRow("-1")]
    [DataRow("1.5")]
    public async Task Presigned_ExpiresOutsideOneToSevenDays_IsForbiddenAsExpiryOutOfRange(string expires)
    {
        var verdict = await JudgeAsync(CreatePolicy(), PresignedGet(PresignedTarget(expires: expires)));

        AssertForbidden(verdict, HttpAuthenticationRefusal.ExpiryOutOfRange);
    }

    [TestMethod]
    public async Task Presigned_WithAChangedParameter_IsForbiddenAsBadSignature()
    {
        var verdict = await JudgeAsync(CreatePolicy(), PresignedGet(PresignedTarget(expires: "86399")));

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
        Assert.AreEqual("Login refused: AWS4-HMAC-SHA256 AKIAIOSFODNN7EXAMPLE", verdict.CheckedLogin?.Note);
    }

    [TestMethod]
    public async Task Presigned_ForAnUnknownKey_IsForbiddenAsUnknownAccount()
    {
        var verdict = await JudgeAsync(CreatePolicy(), PresignedGet(PresignedTarget(credential: "AKIAUNKNOWN%2F20130524%2Fus-east-1%2Fs3%2Faws4_request")));

        AssertForbidden(verdict, HttpAuthenticationRefusal.UnknownAccount);
        Assert.IsNull(verdict.SignatureMismatch);
    }

    // The presigned example's canonical request and string to sign, as the page gives them.
    [TestMethod]
    public async Task PresignedWorkedExample_SignedWithAWrongSecret_CarriesTheCanonicalRequestAndStringToSign()
    {
        var verdict = await JudgeAsync(CreatePolicy(), PresignedGet(PresignedTarget(signature: new string('0', 64))));

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
        Assert.AreEqual(
            string.Join(
                '\n',
                "GET",
                "/test.txt",
                "X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request"
                + "&X-Amz-Date=20130524T000000Z&X-Amz-Expires=86400&X-Amz-SignedHeaders=host",
                "host:examplebucket.s3.amazonaws.com",
                string.Empty,
                "host",
                "UNSIGNED-PAYLOAD"),
            verdict.SignatureMismatch?.CanonicalRequest);
        Assert.AreEqual(
            string.Join(
                '\n',
                "AWS4-HMAC-SHA256",
                "20130524T000000Z",
                "20130524/us-east-1/s3/aws4_request",
                "3bfa292879f6447bbcda7001decf97f4a54dc650c8942174ae0a9121cf58ad04"),
            verdict.SignatureMismatch?.StringToSign);
    }

    // The GET Object example sent with a body and no content hash, so its signature covers the
    // body's hash and is checked only once the body is read.
    [TestMethod]
    public async Task BodyBoundSignature_RefusedAtTheBodyCheck_CarriesTheCanonicalRequestAndStringToSign()
    {
        var request = new HttpAuthenticationRequest(
            "PUT",
            "/test.txt",
            true,
            [
                new("Host", "examplebucket.s3.amazonaws.com"),
                new("x-amz-date", "20130524T000000Z"),
                new("Content-Length", "5"),
                new(
                    "Authorization",
                    "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,"
                    + $"SignedHeaders=host;x-amz-date,Signature={new string('0', 64)}"),
            ]);
        clock.Advance(ExampleDate - clock.GetUtcNow());
        var policy = PolicyFixture.Create(Accounts, clock, httpMethods: [new AwsSigV4AuthenticationMethod(Accounts, clock)]);
        var verdict = await policy.StartHttpConnection(PolicyFixture.Tls).JudgeAsync(request, CancellationToken.None);
        Assert.IsNull(verdict.SignatureMismatch);

        var judgement = verdict.BodyCheck!.JudgeBodyAsync(SHA256.HashData("hello"u8), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var bodyVerdict = await judgement;

        var canonicalRequest = string.Join(
            '\n',
            "PUT",
            "/test.txt",
            string.Empty,
            "host:examplebucket.s3.amazonaws.com",
            "x-amz-date:20130524T000000Z",
            string.Empty,
            "host;x-amz-date",
            "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
        Assert.AreEqual(HttpAuthenticationRefusal.BadSignature, bodyVerdict.Refusal);
        Assert.AreEqual(canonicalRequest, bodyVerdict.SignatureMismatch?.CanonicalRequest);
        Assert.AreEqual(
            string.Join(
                '\n',
                "AWS4-HMAC-SHA256",
                "20130524T000000Z",
                "20130524/us-east-1/s3/aws4_request",
                Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonicalRequest)))),
            bodyVerdict.SignatureMismatch?.StringToSign);
    }

    [TestMethod]
    public async Task BodyNotHashingToTheContentHash_HasNoMismatch()
    {
        // The signature over the sent hash matches; only the body differs.
        var request = Signed(ChunkedPut(ChunkedAuthorization(), Convert.ToHexStringLower(SHA256.HashData("hello"u8))));
        var verdict = await JudgeAsync(CreatePolicy(), request);

        var judgement = verdict.BodyCheck!.JudgeBodyAsync(SHA256.HashData("hellO"u8), CancellationToken.None).AsTask();
        clock.Advance(AuthenticationPolicy.RefusalDelay);
        var bodyVerdict = await judgement;

        Assert.IsNull(verdict.SignatureMismatch);
        Assert.AreEqual(HttpAuthenticationRefusal.PayloadHashMismatch, bodyVerdict.Refusal);
        Assert.IsNull(bodyVerdict.SignatureMismatch);
    }

    [TestMethod]
    [DataRow("AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fec2%2Faws4_request", "AWS4-HMAC-SHA256", "20130524T000000Z", "host", DisplayName = "another service")]
    [DataRow("AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request", "OSC4-HMAC-SHA256", "20130524T000000Z", "host", DisplayName = "another provider's algorithm")]
    [DataRow("AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request", "AWS4-HMAC-SHA256", "20130525T000000Z", "host", DisplayName = "not the scope's date")]
    [DataRow("AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request", "AWS4-HMAC-SHA256", "20130524", "host", DisplayName = "a date that does not read")]
    [DataRow("AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request", "AWS4-HMAC-SHA256", "20130524T000000Z", "range", DisplayName = "host not signed")]
    [DataRow("AKIAIOSFODNN7EXAMPLE", "AWS4-HMAC-SHA256", "20130524T000000Z", "host", DisplayName = "a credential with no scope")]
    public async Task Presigned_Malformed_IsForbiddenAsMalformedCredentials(string credential, string algorithm, string date, string signedHeaders)
    {
        var verdict = await JudgeAsync(
            CreatePolicy(),
            PresignedGet(PresignedTarget(credential, algorithm, date, signedHeaders: signedHeaders), new KeyValuePair<string, string>("Range", "bytes=0-9")));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
    }

    [TestMethod]
    [DataRow("host%3Bhost", DisplayName = "in the same case")]
    [DataRow("host%3BHost", DisplayName = "in another case")]
    public async Task Presigned_SignedHeadersRepeatingAName_IsForbiddenAsMalformedCredentials(string signedHeaders)
    {
        var verdict = await JudgeAsync(CreatePolicy(), PresignedGet(PresignedTarget(signedHeaders: signedHeaders)));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
    }

    [TestMethod]
    [DataRow("/test.txt?X-Amz-Algorithm=AWS4-HMAC-SHA256", DisplayName = "only the algorithm")]
    [DataRow("/test.txt?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-SignedHeaders=host", DisplayName = "no signature")]
    [DataRow("/test.txt?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Signature=" + PresignedSignature, DisplayName = "no signed headers")]
    [DataRow("/test.txt?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-SignedHeaders=host&X-Amz-Signature=" + PresignedSignature, DisplayName = "no credential")]
    [DataRow("/test.txt?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-SignedHeaders=host&X-Amz-Signature=" + PresignedSignature + "&X-Amz-Expires=60", DisplayName = "no date")]
    [DataRow("/test.txt?X-Amz-Algorithm&X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20130524%2Fus-east-1%2Fs3%2Faws4_request&X-Amz-Date=20130524T000000Z&X-Amz-SignedHeaders=host&X-Amz-Signature=" + PresignedSignature, DisplayName = "an empty algorithm")]
    public async Task Presigned_MissingAParameter_IsForbiddenAsMalformedCredentials(string target)
    {
        var verdict = await JudgeAsync(CreatePolicy(), PresignedGet(target));

        AssertForbidden(verdict, HttpAuthenticationRefusal.MalformedCredentials);
    }

    [TestMethod]
    public async Task Presigned_WithAnAuthorizationToo_IsForbiddenAsConflictingCredentialsUndelayed()
    {
        var judgement = CreatePolicy().StartHttpConnection(null).JudgeAsync(
            PresignedGet(PresignedTarget(), new KeyValuePair<string, string>("Authorization", ChunkedAuthorization())),
            CancellationToken.None);

        Assert.IsTrue(judgement.IsCompleted);
        AssertForbidden(await judgement, HttpAuthenticationRefusal.ConflictingCredentials);
    }

    [TestMethod]
    public async Task Presigned_ForCloudStorage_IsNotReadAsAnAwsPresignedUrl()
    {
        var verdict = await JudgeAsync(CreatePolicy(), PresignedGet(PresignedTarget()) with { AwsSigV4Service = GoogleV4HmacAuthenticationMethod.Service });

        AssertForbidden(verdict, HttpAuthenticationRefusal.MissingCredentials);
    }

    [TestMethod]
    public async Task ChunkedWorkedExample_SeedSignature_IsAcceptedWithAChainThatVerifiesEachChunk()
    {
        var verdict = await JudgeAsync(CreatePolicy(), ChunkedPut(ChunkedAuthorization()));

        Assert.AreEqual(ExampleAccessKeyId, verdict.AccountName);
        Assert.IsNull(verdict.BodyCheck);
        var chain = verdict.ChunkSignatures!;
        Assert.IsTrue(chain.VerifyNextChunk(Sha256OfAs(65536), FirstChunkSignature));
        Assert.IsTrue(chain.VerifyNextChunk(Sha256OfAs(1024), SecondChunkSignature));
        Assert.IsTrue(chain.VerifyNextChunk(Sha256OfAs(0), FinalChunkSignature));
    }

    [TestMethod]
    public async Task ChunkedWorkedExample_ATamperedChunk_FailsAndLeavesTheChainWhereItWas()
    {
        var chain = (await JudgeAsync(CreatePolicy(), ChunkedPut(ChunkedAuthorization()))).ChunkSignatures!;
        var tampered = Enumerable.Repeat((byte)'a', 65536).ToArray();
        tampered[100] = (byte)'b';

        Assert.IsFalse(chain.VerifyNextChunk(SHA256.HashData(tampered), FirstChunkSignature));
        Assert.IsFalse(chain.VerifyNextChunk(Sha256OfAs(65536), "not hex"));
        Assert.IsFalse(chain.VerifyNextChunk(Sha256OfAs(1024), SecondChunkSignature));
        Assert.IsTrue(chain.VerifyNextChunk(Sha256OfAs(65536), FirstChunkSignature));
    }

    [TestMethod]
    public async Task ChunkedWorkedExample_WithAWrongSeedSignature_IsForbiddenAsBadSignature()
    {
        var verdict = await JudgeAsync(CreatePolicy(), ChunkedPut(ChunkedAuthorization(new string('0', 64))));

        AssertForbidden(verdict, HttpAuthenticationRefusal.BadSignature);
        Assert.IsNull(verdict.ChunkSignatures);
    }

    [TestMethod]
    public async Task UnsignedTrailerPayload_IsAcceptedWithNoChainAndNoBodyBound()
    {
        // The signature no longer matches the example's, so it is computed with the example's key.
        var request = ChunkedPut(ChunkedAuthorization(), "STREAMING-UNSIGNED-PAYLOAD-TRAILER");
        var verdict = await JudgeAsync(CreatePolicy(), Signed(request));

        Assert.AreEqual(ExampleAccessKeyId, verdict.AccountName);
        Assert.IsNull(verdict.BodyCheck);
        Assert.IsNull(verdict.ChunkSignatures);
    }

    // The trailer page's formulas written out with the BCL: the signing key is four HMACs over the scope's parts,
    // each signature an HMAC of its string to sign, the lines joined by line feeds.
    private static byte[] ExampleSigningKey()
    {
        var key = System.Text.Encoding.UTF8.GetBytes("AWS4" + ExampleSecretAccessKey);
        foreach (var part in new[] { "20130524", "us-east-1", "s3", "aws4_request" })
        {
            key = HMACSHA256.HashData(key, System.Text.Encoding.UTF8.GetBytes(part));
        }

        return key;
    }

    private static string HmacHex(string stringToSign) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(ExampleSigningKey(), System.Text.Encoding.UTF8.GetBytes(stringToSign)));

    private static string ChunkSignatureOf(string previousSignature, byte[] data) =>
        HmacHex(string.Join('\n', "AWS4-HMAC-SHA256-PAYLOAD", "20130524T000000Z", "20130524/us-east-1/s3/aws4_request", previousSignature, Convert.ToHexStringLower(SHA256.HashData([])), Convert.ToHexStringLower(SHA256.HashData(data))));

    private static string TrailerSignatureOf(string finalChunkSignature, string trailerLines) =>
        HmacHex(string.Join('\n', "AWS4-HMAC-SHA256-TRAILER", "20130524T000000Z", "20130524/us-east-1/s3/aws4_request", finalChunkSignature, Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(trailerLines)))));

    private async Task<(IHttpChunkSignatureChain Chain, string Seed)> SignedTrailerChainAsync()
    {
        var request = Signed(ChunkedPut(ChunkedAuthorization(), SignatureV4Check.StreamingSignedPayloadWithTrailer));
        var verdict = await JudgeAsync(CreatePolicy(), request);
        var seed = request.Fields.Single(field => field.Key == "Authorization").Value[^64..];

        Assert.AreEqual(ExampleAccessKeyId, verdict.AccountName);
        Assert.IsNull(verdict.BodyCheck);

        return (verdict.ChunkSignatures!, seed);
    }

    [TestMethod]
    public async Task SignedTrailerPayload_IsAcceptedWithAChainThatVerifiesEachChunkThenTheTrailer()
    {
        var (chain, seed) = await SignedTrailerChainAsync();
        var first = ChunkSignatureOf(seed, "hello"u8.ToArray());
        var last = ChunkSignatureOf(first, []);

        Assert.IsTrue(chain.VerifyNextChunk(SHA256.HashData("hello"u8), first));
        Assert.IsTrue(chain.VerifyNextChunk(SHA256.HashData([]), last));
        Assert.IsTrue(chain.VerifyTrailer(SHA256.HashData("x-amz-checksum-crc32:NhCmhg==\n"u8), TrailerSignatureOf(last, "x-amz-checksum-crc32:NhCmhg==\n")));
    }

    [TestMethod]
    public async Task SignedTrailerPayload_ATamperedTrailerLineOrSignature_IsRefused()
    {
        var (chain, seed) = await SignedTrailerChainAsync();
        var last = ChunkSignatureOf(seed, []);
        var good = TrailerSignatureOf(last, "x-amz-checksum-crc32:NhCmhg==\n");

        Assert.IsTrue(chain.VerifyNextChunk(SHA256.HashData([]), last));
        Assert.IsFalse(chain.VerifyTrailer(SHA256.HashData("x-amz-checksum-crc32:AAAAAA==\n"u8), good));
        Assert.IsFalse(chain.VerifyTrailer(SHA256.HashData("x-amz-checksum-crc32:NhCmhg==\n"u8), new string('0', 64)));
        Assert.IsFalse(chain.VerifyTrailer(SHA256.HashData("x-amz-checksum-crc32:NhCmhg==\n"u8), "not hex"));
        Assert.IsTrue(chain.VerifyTrailer(SHA256.HashData("x-amz-checksum-crc32:NhCmhg==\n"u8), good));
    }

    [TestMethod]
    public async Task SignedTrailerPayload_NullArguments_Throw()
    {
        var (chain, _) = await SignedTrailerChainAsync();

        Assert.ThrowsExactly<ArgumentNullException>(() => chain.VerifyTrailer(SHA256.HashData([]), null!));
    }

    [TestMethod]
    public async Task StreamingPayloadForAnotherService_IsBoundAsAHashAndFailsOnTheBody()
    {
        var request = ChunkedPut(ChunkedAuthorization().Replace("/s3/", "/ec2/", StringComparison.Ordinal), service: "ec2");
        var verdict = await JudgeAsync(CreatePolicy(), Signed(request));

        Assert.IsNotNull(verdict.BodyCheck);
        Assert.IsNull(verdict.ChunkSignatures);
    }

    // The request re-signed with the example's key, as a client would sign it.
    private static HttpAuthenticationRequest Signed(HttpAuthenticationRequest request)
    {
        var authorization = AwsSigV4Authorization.TryParse(request.Fields.Single(field => field.Key == "Authorization").Value["AWS4-HMAC-SHA256 ".Length..])!;
        var payloadHash = request.Fields.Single(field => field.Key == "x-amz-content-sha256").Value;
        var canonicalRequest = AwsSigV4CanonicalRequest.Build(request, authorization, payloadHash, authorization.Service != "s3")!;
        var signature = AwsSigV4Calculation.ComputeSignature(authorization, "20130524T000000Z", canonicalRequest, System.Text.Encoding.UTF8.GetBytes(ExampleSecretAccessKey));
        var credentials = request.Fields.Single(field => field.Key == "Authorization").Value[..^64] + Convert.ToHexStringLower(signature);

        return request with { Fields = [.. request.Fields.Select(field => field.Key == "Authorization" ? new KeyValuePair<string, string>(field.Key, credentials) : field)] };
    }
}
