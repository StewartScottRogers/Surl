using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Each login refusal ADR-0110 decision 3.4 gives, for Bearer tokens and anonymous requests (RFC
/// 6750; "HTTP status and error codes").
/// </summary>
[TestClass]
public sealed class GcsLoginJudgeTests
{
    private const string BearerChallengeField = "WWW-Authenticate: Bearer realm=\"storage\"\r\n";
    private const string InvalidTokenChallengeField = "WWW-Authenticate: Bearer realm=\"storage\", error=\"invalid_token\"\r\n";

    [TestMethod]
    public async Task ServeAsync_NoTokenWithAnonymousOff_Is401AuthenticationRequiredWithTheBearerChallenge()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt");

        Assert.AreEqual(GcsServerHarness.ExpectedError("401 Unauthorized", "AuthenticationRequired", "Authentication required.", BearerChallengeField), response);
    }

    [TestMethod]
    public async Task ServeAsync_NoTokenUnderAllowAnonymous_ServesTheRead()
    {
        var harness = new GcsServerHarness(allowsAnonymous: true);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync("GET", "/photos/a.txt");

        StringAssert.EndsWith(response, "\r\n\r\nhello world");
    }

    [TestMethod]
    [DataRow("PUT", DisplayName = "PUT Object")]
    [DataRow("DELETE", DisplayName = "DELETE Object")]
    public async Task ServeAsync_AnonymousWrite_Is401AuthenticationRequired(string method)
    {
        var harness = new GcsServerHarness(allowsAnonymous: true, exposure: GcsServerHarness.Writable);
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        var response = await harness.SendAsync(method, "/photos/a.txt", "Content-Length: 0");

        Assert.AreEqual(GcsServerHarness.ExpectedError("401 Unauthorized", "AuthenticationRequired", "Authentication required.", BearerChallengeField), response);
        Assert.AreEqual("hello world", harness.ReadFile("photos/a.txt"));
    }

    [TestMethod]
    [DataRow("Basic YWxpY2U6cHc=", DisplayName = "Basic")]
    [DataRow("AWS4-HMAC-SHA256 Credential=x", DisplayName = "AWS SigV4")]
    [DataRow("GOOG1 id:signature", DisplayName = "the V2 GOOG1 form")]
    [DataRow("Bearer ", DisplayName = "Bearer with no token")]
    public async Task ServeAsync_AnotherScheme_Is400InvalidAuthentication(string authorization)
    {
        var harness = new GcsServerHarness(allowsAnonymous: true);
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("GET", "/photos/a.txt", $"Authorization: {authorization}");

        Assert.AreEqual(GcsServerHarness.ExpectedError("400 Bad Request", "InvalidAuthentication", "The provided authentication header is invalid."), response);
        Assert.IsEmpty(harness.Policy.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_TokenThatIsNoAccount_Is401InvalidAuthenticationWithTheInvalidTokenChallenge()
    {
        var harness = new GcsServerHarness(allowsAnonymous: true);
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("GET", "/photos/a.txt", "Authorization: Bearer wrong");

        Assert.AreEqual(GcsServerHarness.ExpectedError("401 Unauthorized", "InvalidAuthentication", "The provided authentication header is invalid.", InvalidTokenChallengeField), response);
    }

    [TestMethod]
    public async Task ServeAsync_NoTokenForAnUnlistedRequest_IsRefusedBeforeTheOperationIsJudged()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");

        var response = await harness.SendAsync("POST", "/photos/a.txt", "Content-Length: 0");

        StringAssert.Contains(response, "<Code>AuthenticationRequired</Code>");
    }

    [TestMethod]
    public async Task ServeAsync_Request_IsJudgedAsAWriteOnlyWhenItIsNoRead()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");

        await harness.SendWithBearerAsync("HEAD", "/photos/a.txt");
        await harness.SendWithBearerAsync("DELETE", "/photos/a.txt");

        Assert.IsFalse(harness.Policy.Requests[0].IsWrite);
        Assert.IsTrue(harness.Policy.Requests[1].IsWrite);
        Assert.AreEqual("DELETE", harness.Policy.Requests[1].Method);
        Assert.AreEqual("/photos/a.txt", harness.Policy.Requests[1].Target);
    }

    [TestMethod]
    public async Task ServeAsync_BearerRequest_IsJudgedWithNoSignedService()
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");

        await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        Assert.IsNull(harness.Policy.Requests[0].AwsSigV4Service);
    }

    [TestMethod]
    public async Task ServeAsync_SignedGetWorkedByHandInBL430_IsServedAsTheAccessIdsAccount()
    {
        var harness = new GcsServerHarness();
        harness.Policy.SignedAccounts[GcsSignedRequests.WorkedGetAuthorization] = GcsSignedRequests.AccessId;
        harness.CreateFolder("example-bucket/cat-pics");
        harness.WriteFile("example-bucket/cat-pics/tabby.jpeg", "tabby");

        var response = await harness.SendRawAsync(GcsSignedRequests.WorkedGet);

        StringAssert.StartsWith(response, "HTTP/1.1 200 OK\r\n");
        StringAssert.EndsWith(response, "\r\n\r\ntabby");
        var judged = harness.Policy.Requests.Single();
        Assert.AreEqual(GcsLoginJudge.SignedService, judged.AwsSigV4Service);
        Assert.AreEqual("/example-bucket/cat-pics/tabby.jpeg", judged.Target);
        CollectionAssert.Contains(judged.Fields.ToList(), new KeyValuePair<string, string>("x-goog-date", "20191201T190859Z"));
        CollectionAssert.Contains(harness.Log.Notes.ToList(), $"Login accepted: GOOG4-HMAC-SHA256 {GcsSignedRequests.AccessId}");
        Assert.IsEmpty(harness.Policy.JudgedBodyHashes);
    }

    [TestMethod]
    [DataRow("goog4-hmac-sha256 Credential=x", DisplayName = "lower case")]
    [DataRow("GOOG4-HMAC-SHA256", DisplayName = "the scheme alone")]
    public async Task ServeAsync_SignatureSchemeInAnyCase_IsJudgedAsASignedRequest(string authorization)
    {
        var harness = new GcsServerHarness();
        harness.CreateFolder("photos");

        await harness.SendAsync("GET", "/photos/a.txt", $"Authorization: {authorization}");

        Assert.AreEqual(GcsLoginJudge.SignedService, harness.Policy.Requests.Single().AwsSigV4Service);
    }

    [TestMethod]
    [DataRow(HttpAuthenticationRefusal.MalformedCredentials, "400 Bad Request", "InvalidArgument", "Invalid argument.", DisplayName = "malformed")]
    [DataRow(HttpAuthenticationRefusal.MissingRequestDate, "400 Bad Request", "MissingSecurityHeader", "Your request was missing a required header.", DisplayName = "no date")]
    [DataRow(HttpAuthenticationRefusal.MissingPayloadHash, "400 Bad Request", "MissingSecurityHeader", "Your request was missing a required header.", DisplayName = "no content hash")]
    [DataRow(HttpAuthenticationRefusal.UnknownAccount, "403 Forbidden", "InvalidAccessKeyId", "The access key ID you provided does not exist in our records.", DisplayName = "unknown access ID")]
    [DataRow(HttpAuthenticationRefusal.BadSignature, "403 Forbidden", "SignatureDoesNotMatch", "The request signature we calculated does not match the signature you provided.", DisplayName = "bad signature")]
    [DataRow(HttpAuthenticationRefusal.ClockSkew, "403 Forbidden", "RequestTimeTooSkewed", "The difference between the request time and the server's time is too large.", DisplayName = "skewed")]
    public async Task ServeAsync_SignedRequestRefused_IsTheErrorDecision34Gives(HttpAuthenticationRefusal refusal, string status, string code, string message)
    {
        var harness = new GcsServerHarness();
        harness.Policy.SignatureRefusals[GcsSignedRequests.WorkedGetAuthorization] = refusal;
        harness.CreateFolder("example-bucket/cat-pics");
        harness.WriteFile("example-bucket/cat-pics/tabby.jpeg", "tabby");

        var response = await harness.SendRawAsync(GcsSignedRequests.WorkedGet);

        Assert.AreEqual(GcsServerHarness.ExpectedError(status, code, message), response);
        CollectionAssert.Contains(harness.Log.Notes.ToList(), "Login refused: GOOG4-HMAC-SHA256 refused");
    }

    [TestMethod]
    public async Task ServeAsync_SignedGetWithAWrongSignature_Is403SignatureDoesNotMatch()
    {
        var harness = new GcsServerHarness();
        harness.Policy.SignedAccounts[GcsSignedRequests.WorkedGetAuthorization] = GcsSignedRequests.AccessId;
        harness.CreateFolder("example-bucket/cat-pics");
        harness.WriteFile("example-bucket/cat-pics/tabby.jpeg", "tabby");
        var tampered = GcsSignedRequests.WorkedGet.Replace(GcsSignedRequests.WorkedGetSignature, new string('0', 64), StringComparison.Ordinal);

        var response = await harness.SendRawAsync(tampered);

        Assert.AreEqual(GcsServerHarness.ExpectedError("403 Forbidden", "SignatureDoesNotMatch", "The request signature we calculated does not match the signature you provided."), response);
    }

    [TestMethod]
    [DataRow("hello world", "200 OK", DisplayName = "the body signed")]
    [DataRow("hello WORLD", "400 Bad Request", DisplayName = "another body")]
    public async Task ServeAsync_SignedGetWithABody_ReadsAndChecksTheBodyBeforeAnswering(string body, string status)
    {
        var harness = new GcsServerHarness();
        harness.Policy.SignedAccounts[GcsSignedRequests.PutAuthorization] = GcsSignedRequests.AccessId;
        harness.CreateFolder("example-bucket");
        harness.WriteFile("example-bucket/hello.txt", "stored");

        var response = await harness.SendRawAsync(GcsSignedRequests.Request("GET", body));

        StringAssert.StartsWith(response, $"HTTP/1.1 {status}\r\n");
        Assert.AreEqual(GcsSignedRequests.Sha256Hex(body), Convert.ToHexStringLower(harness.Policy.JudgedBodyHashes.Single()));
        Assert.AreEqual(body == "hello world", response.EndsWith("\r\n\r\nstored", StringComparison.Ordinal));
        Assert.AreEqual(body != "hello world", response.Contains("<Code>BadDigest</Code>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ErrorOf_UnsupportedScheme_Is400InvalidAuthentication()
    {
        var error = GcsLoginJudge.ErrorOf(HttpAuthenticationRefusal.UnsupportedScheme);

        Assert.AreSame(GcsError.UnsupportedAuthentication, error);
    }

    [TestMethod]
    [DataRow(HttpAuthenticationRefusal.PayloadHashMismatch, "BadDigest", DisplayName = "body not hashing to x-goog-content-sha256")]
    [DataRow(HttpAuthenticationRefusal.MissingCredentials, "AccessDenied", DisplayName = "a reason the table does not name")]
    public void ErrorOf_Refusal_IsItsCode(HttpAuthenticationRefusal refusal, string code)
    {
        var error = GcsLoginJudge.ErrorOf(refusal);

        Assert.AreEqual(code, error.Code);
    }
}
