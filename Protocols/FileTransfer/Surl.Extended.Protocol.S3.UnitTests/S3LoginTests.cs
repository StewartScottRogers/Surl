using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The S3 server's Signature Version 4 check (ADR-0108 decision 4): each request judged as one for
/// the <c>s3</c> service, each refusal answered with decision 4.3's status and code, the signed
/// account the caller of every store call. The signature arithmetic is <c>Surl.Authentication</c>'s
/// and is tested there against the API Reference's worked GET-object example
/// (<c>AwsSigV4ServiceSignatureTests</c>); here a hand-written policy gives each verdict.
/// </summary>
[TestClass]
public sealed class S3LoginTests
{
    // The worked GET-object example's Authorization, from the Amazon S3 API Reference.
    private const string ExampleAuthorization =
        "AWS4-HMAC-SHA256 Credential=AKIAIOSFODNN7EXAMPLE/20130524/us-east-1/s3/aws4_request,"
        + "SignedHeaders=host;range;x-amz-content-sha256;x-amz-date,Signature=f0e8bdb87c964420e857bd35b5d6ed310bd44f0170aba48dd91039c6036bdb41";

    private const string ExampleAuthorizationField = "Authorization: " + ExampleAuthorization;

    private static S3ServerHarness HarnessWithBucket(UnitTestUnsignedRequests unsignedRequests = UnitTestUnsignedRequests.Refused, IReadOnlyList<ContentShare>? shares = null)
    {
        var harness = new S3ServerHarness(exposure: S3ServerHarness.Writable, shares: shares, policy: new UnitTestS3AuthenticationPolicy(unsignedRequests));
        harness.CreateFolder("bucket");
        harness.WriteFile("bucket/a.txt", "hello world");

        return harness;
    }

    private static string ErrorBody(string code, string message, string resource) =>
        $"<Code>{code}</Code><Message>{message}</Message><Resource>{resource}</Resource><RequestId>{{RequestId}}</RequestId>";

    [TestMethod]
    public async Task ServeAsync_SignedRequest_IsJudgedAsOneForTheS3Service()
    {
        var harness = HarnessWithBucket();

        var response = await harness.SendAsync("GET", "/bucket/a.txt", ExampleAuthorizationField);

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        var judged = harness.Policy.Requests.Single();
        Assert.AreEqual("s3", judged.AwsSigV4Service);
        Assert.AreEqual("GET", judged.Method);
        Assert.AreEqual("/bucket/a.txt", judged.Target);
        Assert.IsFalse(judged.IsWrite);
        CollectionAssert.Contains(harness.Log.Notes.ToArray(), "Login accepted: AWS4-HMAC-SHA256 AKIAIOSFODNN7EXAMPLE");
    }

    [TestMethod]
    public async Task ServeAsync_NoAuthorizationWithAnonymousOff_Is403AccessDenied()
    {
        var harness = HarnessWithBucket();

        var response = await harness.SendAsync("GET", "/bucket/a.txt");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(response, "403 Forbidden", ErrorBody("AccessDenied", "Access Denied", "/bucket/a.txt")),
            response);
    }

    [TestMethod]
    [DataRow(HttpAuthenticationRefusal.UnknownAccount, "403 Forbidden", "InvalidAccessKeyId", "The AWS access key Id you provided does not exist in our records.")]
    [DataRow(HttpAuthenticationRefusal.BadSignature, "403 Forbidden", "SignatureDoesNotMatch", "The request signature we calculated does not match the signature you provided. Check your key and signing method.")]
    [DataRow(HttpAuthenticationRefusal.ClockSkew, "403 Forbidden", "RequestTimeTooSkewed", "The difference between the request time and the server's time is too large.")]
    [DataRow(HttpAuthenticationRefusal.UnsupportedScheme, "400 Bad Request", "InvalidArgument", "Unsupported Authorization Type")]
    [DataRow(HttpAuthenticationRefusal.MalformedCredentials, "400 Bad Request", "AuthorizationHeaderMalformed", "The authorization header that you provided is not valid.")]
    [DataRow(HttpAuthenticationRefusal.MissingPayloadHash, "400 Bad Request", "InvalidRequest", "Missing required header for this request: x-amz-content-sha256")]
    [DataRow(HttpAuthenticationRefusal.MissingCredentials, "403 Forbidden", "AccessDenied", "Access Denied")]
    public async Task ServeAsync_RefusedSignature_IsAnsweredWithItsStatusAndCode(HttpAuthenticationRefusal refusal, string status, string code, string message)
    {
        var harness = HarnessWithBucket();
        harness.Policy.Refusals[ExampleAuthorization] = refusal;

        var response = await harness.SendAsync("GET", "/bucket/a.txt", ExampleAuthorizationField);

        Assert.AreEqual(S3ServerHarness.ExpectedError(response, status, ErrorBody(code, message, "/bucket/a.txt")), response);
        CollectionAssert.Contains(harness.Log.Notes.ToArray(), "Login refused: AWS4-HMAC-SHA256 AKIAIOSFODNN7EXAMPLE");
    }

    [TestMethod]
    public async Task ServeAsync_RefusedSignature_IsRefusedBeforeTheOperation()
    {
        var harness = HarnessWithBucket();
        harness.Policy.Refusals[ExampleAuthorization] = HttpAuthenticationRefusal.BadSignature;

        var response = await harness.SendAsync("DELETE", "/bucket/a.txt", ExampleAuthorizationField);

        StringAssert.Contains(response, "<Code>SignatureDoesNotMatch</Code>");
        Assert.AreEqual("hello world", harness.ReadFile("bucket/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWhoseBodyIsNotTheSignedHash_Is400XAmzContentSha256MismatchAndNotStored()
    {
        var harness = HarnessWithBucket();

        var response = await harness.SendWithBodyAsync(
            "PUT",
            "/bucket/a.txt",
            "changed body",
            ExampleAuthorizationField,
            "x-amz-content-sha256: " + UnitTestS3AuthenticationPolicy.ContentSha256Of("signed body"));

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(
                response,
                "400 Bad Request",
                ErrorBody("XAmzContentSHA256Mismatch", "The provided 'x-amz-content-sha256' header does not match what was computed.", "/bucket/a.txt")),
            response);
        Assert.AreEqual("hello world", harness.ReadFile("bucket/a.txt"));
        CollectionAssert.AreEqual(SHA256.HashData(Encoding.UTF8.GetBytes("changed body")), harness.Policy.JudgedBodyHashes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWhoseBodyIsTheSignedHash_IsStored()
    {
        var harness = HarnessWithBucket();

        var response = await harness.SendWithBodyAsync(
            "PUT",
            "/bucket/b.txt",
            "signed body",
            ExampleAuthorizationField,
            "x-amz-content-sha256: " + UnitTestS3AuthenticationPolicy.ContentSha256Of("signed body"));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        Assert.AreEqual("signed body", harness.ReadFile("bucket/b.txt"));
        Assert.IsTrue(harness.Policy.Requests.Single().IsWrite);
        CollectionAssert.Contains(harness.Log.Notes.ToArray(), "Login accepted: AWS4-HMAC-SHA256 AKIAIOSFODNN7EXAMPLE");
    }

    [TestMethod]
    public async Task ServeAsync_PutObjectWhoseBodyIsTheSignedHashButNotTheContentMd5_IsBadDigest()
    {
        var harness = HarnessWithBucket();

        var response = await harness.SendWithBodyAsync(
            "PUT",
            "/bucket/b.txt",
            "signed body",
            ExampleAuthorizationField,
            "x-amz-content-sha256: " + UnitTestS3AuthenticationPolicy.ContentSha256Of("signed body"),
            "Content-MD5: " + Convert.ToBase64String(MD5.HashData(Encoding.UTF8.GetBytes("other"))));

        StringAssert.Contains(response, "<Code>BadDigest</Code>");
    }

    [TestMethod]
    [DataRow("GET", "/bucket/a.txt", DisplayName = "GetObject")]
    [DataRow("DELETE", "/bucket/a.txt", DisplayName = "an operation not served")]
    public async Task ServeAsync_UnusedBodyThatIsNotTheSignedHash_IsReadAndRefused(string method, string target)
    {
        var harness = HarnessWithBucket();

        var response = await harness.SendWithBodyAsync(
            method, target, "changed body", ExampleAuthorizationField, "x-amz-content-sha256: " + UnitTestS3AuthenticationPolicy.ContentSha256Of("signed body"));

        StringAssert.Contains(response, "<Code>XAmzContentSHA256Mismatch</Code>");
        CollectionAssert.AreEqual(SHA256.HashData(Encoding.UTF8.GetBytes("changed body")), harness.Policy.JudgedBodyHashes.Single());
    }

    [TestMethod]
    public async Task ServeAsync_UnusedBodyThatIsTheSignedHash_IsServed()
    {
        var harness = HarnessWithBucket();

        var response = await harness.SendWithBodyAsync(
            "GET", "/bucket/a.txt", "signed body", ExampleAuthorizationField, "x-amz-content-sha256: " + UnitTestS3AuthenticationPolicy.ContentSha256Of("signed body"));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "hello world");
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousGetUnderAllowAnonymous_IsServed()
    {
        var harness = HarnessWithBucket(UnitTestUnsignedRequests.Anonymous);

        var response = await harness.SendAsync("GET", "/bucket/a.txt");

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "hello world");
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousListingUnderAllowAnonymous_NamesTheAnonymousOwner()
    {
        var harness = HarnessWithBucket(UnitTestUnsignedRequests.Anonymous);

        var response = await harness.SendAsync("GET", "/bucket?list-type=2&fetch-owner=true");

        StringAssert.Contains(response, "<Owner><ID>anonymous</ID><DisplayName>anonymous</DisplayName></Owner>");
    }

    [TestMethod]
    public async Task ServeAsync_AnonymousPutUnderAllowAnonymous_Is403AccessDeniedAndNotStored()
    {
        var harness = HarnessWithBucket(UnitTestUnsignedRequests.Anonymous);

        var response = await harness.SendWithBodyAsync("PUT", "/bucket/a.txt", "changed");

        Assert.AreEqual(
            S3ServerHarness.ExpectedError(response, "403 Forbidden", ErrorBody("AccessDenied", "Access Denied", "/bucket/a.txt")),
            response);
        Assert.AreEqual("hello world", harness.ReadFile("bucket/a.txt"));
    }

    [TestMethod]
    public async Task ServeAsync_SignedAccount_IsTheCallerTheShareAccessCheckSees()
    {
        var shareFileSystem = new InMemoryContentFileSystem(new FixedTimeProvider(S3ServerHarness.Now));
        var share = new ContentShare("shared", S3ServerHarness.Root, shareFileSystem)
        {
            Grants = new Dictionary<string, ContentShareAccess> { [UnitTestS3AuthenticationPolicy.ExampleAccessKeyId] = ContentShareAccess.ReadWrite },
        };
        var granted = HarnessWithBucket(shares: [share]);
        var other = new S3ServerHarness(
            exposure: S3ServerHarness.Writable,
            shares: [share],
            policy: new UnitTestS3AuthenticationPolicy(UnitTestUnsignedRequests.Refused) { AccountName = "someone-else" });

        var grantedPut = await granted.SendWithBodyAsync("PUT", "/shared/a.txt", "hello", ExampleAuthorizationField);
        var otherPut = await other.SendWithBodyAsync("PUT", "/shared/b.txt", "hello", ExampleAuthorizationField);
        var otherGet = await other.SendAsync("GET", "/shared/a.txt", ExampleAuthorizationField);

        StringAssert.StartsWith(grantedPut, "HTTP/1.1 200 OK\r\n");
        StringAssert.Contains(otherPut, "<Code>NoSuchBucket</Code>");
        StringAssert.Contains(otherGet, "<Code>NoSuchBucket</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_EachConnection_StartsASessionWithItsTlsSession()
    {
        var harness = HarnessWithBucket();
        var tls = new TlsSession(SslProtocols.Tls13, TlsCipherSuite.TLS_AES_128_GCM_SHA256, null, null, null);
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes($"GET /bucket/a.txt HTTP/1.1\r\nHost: s3.test\r\n{ExampleAuthorizationField}\r\nConnection: close\r\n\r\n")], initialTlsSession: tls);

        await harness.Server.ServeAsync(connection, harness.Context());

        Assert.AreSame(tls, harness.Policy.StartedConnections.Single());
    }

    [TestMethod]
    public void ErrorOf_NoReason_IsAccessDenied()
    {
        Assert.AreSame(S3Error.AccessDenied, S3Login.ErrorOf(null));
    }

    [TestMethod]
    [DataRow(HttpAuthenticationRefusal.SignatureExpired, "AccessDenied", 403, "Request has expired")]
    [DataRow(HttpAuthenticationRefusal.ExpiryOutOfRange, "InvalidArgument", 400, "X-Amz-Expires must be a number of seconds from 1 to 604800")]
    [DataRow(HttpAuthenticationRefusal.ConflictingCredentials, "InvalidArgument", 400, "Only one auth mechanism allowed; only the X-Amz-Algorithm query parameter, Signature query string parameter or the Authorization header should be specified")]
    public void ErrorOf_PresignedUrlRefusals_HaveTheirErrors(HttpAuthenticationRefusal refusal, string code, int status, string message)
    {
        var error = S3Login.ErrorOf(refusal);

        Assert.AreEqual(code, error.Code);
        Assert.AreEqual(status, error.Status.Code);
        Assert.AreEqual(message, error.Message);
    }

    [TestMethod]
    public async Task ServeAsync_PresignedGetObject_IsReadAsGetObjectWithItsSignatureParametersSetAside()
    {
        // The policy stands in for Surl.Authentication's presigned check, which has no Authorization to read.
        var harness = HarnessWithBucket(UnitTestUnsignedRequests.LoggedIn);

        var response = await harness.SendAsync(
            "GET",
            "/bucket/a.txt?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20261105%2Fus-east-1%2Fs3%2Faws4_request"
                + "&X-Amz-Date=20261105T120000Z&X-Amz-Expires=60&X-Amz-SignedHeaders=host&X-Amz-Signature=" + new string('0', 64));

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "\r\n\r\nhello world");
        StringAssert.Contains(harness.Policy.Requests.Single().Target, "X-Amz-Signature=");
    }
}
