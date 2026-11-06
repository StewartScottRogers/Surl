namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// How a WebTransport CONNECT's login is judged (ADR-0117 decision 7; ADR-0032 sections 4 and 6),
/// over the in-memory QUIC test connection, every client byte written from the specifications.
/// </summary>
[TestClass]
public sealed class WebTransportConnectAuthenticationTests
{
    public TestContext TestContext { get; set; } = null!;

    private CancellationToken Token => TestContext.CancellationToken;

    [TestMethod]
    public async Task RefuseAsync_ConnectWithoutALogin_IsAnswered401WithTheRequestBoundChallengesAndFin()
    {
        using var client = new Http3TestQuicClient();
        var policy = new UnitTestAuthenticationPolicy(allowAnonymous: false);
        var (log, _) = await UnitTestWebTransport.StartAsync(client, Token, authenticationPolicy: policy);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token);
        await client.UntilAsync(() => client.Response(0).Fin, Token);

        Assert.AreEqual("401", response.Status);
        CollectionAssert.AreEqual(new[] { "www-authenticate: Basic realm=\"surl\"" }, response.RegularFields, "NTLM is bound to a connection, which WebTransport cannot offer.");
        Assert.Contains("WebTransport CONNECT 0 to /echo was answered 401.", log.Notes);
    }

    [TestMethod]
    public async Task RefuseAsync_ConnectWithTheAccountsLogin_IsAccepted()
    {
        using var client = new Http3TestQuicClient();
        var policy = new UnitTestAuthenticationPolicy(allowAnonymous: false);
        await UnitTestWebTransport.StartAsync(client, Token, authenticationPolicy: policy);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/echo", ("authorization", UnitTestAuthenticationPolicy.GoodAuthorization));

        Assert.AreEqual("200", response.Status);
        var request = policy.Requests.Single();
        Assert.AreEqual("CONNECT", request.Method);
        Assert.AreEqual("/echo", request.Target);
        Assert.IsFalse(request.IsWrite);
    }

    [TestMethod]
    public async Task RefuseAsync_ConnectWithAWrongLogin_IsAnswered403()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token, authenticationPolicy: new UnitTestAuthenticationPolicy(allowAnonymous: false));

        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/echo", ("authorization", "Basic dXNlcjp3cm9uZw=="));
        await client.UntilAsync(() => client.Response(0).Fin, Token);

        Assert.AreEqual("403", response.Status);
        Assert.IsEmpty(response.RegularFields, "Neither the account nor the cause is named.");
    }

    [TestMethod]
    public async Task RefuseAsync_AnonymousConnectUnderAllowAnonymous_IsAccepted()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token, authenticationPolicy: new UnitTestAuthenticationPolicy(allowAnonymous: true));

        var response = await UnitTestWebTransport.ConnectAsync(client, Token);

        Assert.AreEqual("200", response.Status);
    }

    [TestMethod]
    public async Task RefuseAsync_NtlmCredentials_AreNotJudgedAndAreAnswered401WithTheRequestBoundChallenges()
    {
        using var client = new Http3TestQuicClient();
        var policy = new UnitTestAuthenticationPolicy(allowAnonymous: false);
        await UnitTestWebTransport.StartAsync(client, Token, authenticationPolicy: policy);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/echo", ("authorization", "NTLM TlRMTVNTUAABAAAA"));

        Assert.AreEqual("401", response.Status);
        CollectionAssert.AreEqual(new[] { "www-authenticate: Basic realm=\"surl\"" }, response.RegularFields);
        Assert.IsFalse(policy.Requests.Single().Fields.Any(field => field.Key == "authorization"), "The NTLM message never reaches the policy.");
    }

    [TestMethod]
    public async Task RefuseAsync_NegotiateCredentialsUnderAllowAnonymous_AreAnswered401NotServedAsAnonymous()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token, authenticationPolicy: new UnitTestAuthenticationPolicy(allowAnonymous: true));

        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/echo", ("authorization", "Negotiate YII="));

        Assert.AreEqual("401", response.Status);
        Assert.IsEmpty(response.RegularFields);
    }

    [TestMethod]
    public async Task RefuseAsync_ConnectWithoutALoginToAnUnservedPath_IsAnswered401BeforeThePathIsJudged()
    {
        using var client = new Http3TestQuicClient();
        await UnitTestWebTransport.StartAsync(client, Token, authenticationPolicy: new UnitTestAuthenticationPolicy(allowAnonymous: false));

        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/other");

        Assert.AreEqual("401", response.Status, "ADR-0117 decision 1: an unauthenticated client learns nothing about the paths served.");
    }

    [TestMethod]
    public async Task RefuseAsync_ConnectFromAnotherOrigin_IsAnswered403BeforeTheLoginIsJudged()
    {
        using var client = new Http3TestQuicClient();
        var policy = new UnitTestAuthenticationPolicy(allowAnonymous: false);
        await UnitTestWebTransport.StartAsync(client, Token, authenticationPolicy: policy);

        var response = await UnitTestWebTransport.ConnectAsync(client, Token, "/echo", ("origin", "https://elsewhere.example"));

        Assert.AreEqual("403", response.Status);
        Assert.IsEmpty(policy.Requests);
    }
}
