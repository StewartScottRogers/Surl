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
}
