using Surl.Protocol.Abstractions;
using static Surl.Protocol.Http.HttpServerHarness;

namespace Surl.Protocol.Http;

/// <summary>
/// Logins over HTTP/3 (ADR-0082 decision 9): NTLM and Negotiate, bound to the connection, are reset
/// and stopped <c>H3_VERSION_FALLBACK</c>; logins bound to the request are answered as over HTTP/1.1.
/// </summary>
public sealed partial class Http3ConnectionServerTests
{
    private const ulong H3VersionFallback = 0x110;

    [TestMethod]
    [DataRow("NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==")]
    [DataRow("ntlm TlRM")]
    public async Task ServeAsync_StreamWhoseAuthorizationIsNtlm_IsResetAndStoppedVersionFallbackUnjudgedAndTheConnectionCarriesOn(string authorization) =>
        await AssertResetVersionFallbackUnjudgedAsync(authorization);

    [TestMethod]
    [DataRow("Negotiate YIIBhwYGKwYBBQUCoIIBezCCAXegDTALBgkqhkiG9xIBAgI=")]
    public async Task ServeAsync_StreamWhoseAuthorizationIsNegotiate_IsResetAndStoppedVersionFallbackUnjudgedAndTheConnectionCarriesOn(string authorization) =>
        await AssertResetVersionFallbackUnjudgedAsync(authorization);

    [TestMethod]
    [DataRow(new[] { "NTLM" })]
    [DataRow(new[] { "Negotiate" })]
    [DataRow(new[] { "Negotiate", "NTLM" })]
    public async Task ServeAsync_RequestWhoseOnlyChallengesAreNtlmOrNegotiate_IsResetAndStoppedVersionFallbackAndTheConnectionCarriesOn(string[] challenges)
    {
        var policy = new UnitTestAuthenticationPolicy(request => request.Target == "/file.txt" ? Challenge(challenges) : Proceed());
        using var client = new Http3TestClient { ProtocolServerPolicy = policy };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.ResetCode(0) is not null && client.StopSendingCode(0) is not null, Token);
        client.Send(4, client.Request("GET", "/other.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(4), Token);

        Assert.AreEqual(H3VersionFallback, client.ResetCode(0));
        Assert.AreEqual(H3VersionFallback, client.StopSendingCode(0));
        Assert.IsEmpty(client.StreamSent(0).Data, "No HEADERS went out before the reset.");
        Assert.AreEqual("404", client.Response(4).Status);
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "Stream 0 was reset with VersionFallback: the only logins it could be challenged for are NTLM or Negotiate, which HTTP/3 does not carry.");
        Assert.IsNull(client.CloseCode);
    }

    [TestMethod]
    [DataRow(new[] { "Basic realm=\"surl\", charset=\"UTF-8\"" })]
    [DataRow(new[] { "NTLM", "Basic realm=\"surl\"" })]
    public async Task ServeAsync_ChallengeOfferingALoginBoundToTheRequest_IsAnswered401AsOverHttp11(string[] challenges)
    {
        using var client = new Http3TestClient { ProtocolServerPolicy = new UnitTestAuthenticationPolicy(_ => Challenge(challenges)) };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.AreEqual("401", response.Status);
        Assert.IsNull(client.ResetCode(0));
        CollectionAssert.AreEqual(challenges.Select(challenge => $"www-authenticate: {challenge}").ToArray(), response.RegularFields.Where(field => field.StartsWith("www-authenticate: ", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task ServeAsync_BasicLogin_IsAnswered200AsOverHttp11()
    {
        const string authorization = "Basic YTpi";
        var policy = new UnitTestAuthenticationPolicy(request =>
            request.Fields.Any(field => field.Key == "authorization" && field.Value == authorization) ? Proceed() : Challenge(["Basic realm=\"surl\""]));
        using var client = new Http3TestClient { ProtocolServerPolicy = policy };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt", ("authorization", authorization)), fin: true);
        await client.UntilAsync(() => client.HasEnded(0), Token);

        var response = client.Response(0);
        Assert.AreEqual("200", response.Status);
        Assert.AreEqual(FileBody, response.BodyText);
        Assert.IsNull(client.ResetCode(0));
        Assert.HasCount(1, policy.JudgedRequests);
    }

    private static HttpAuthenticationVerdict Proceed() => new(HttpAuthenticationOutcome.Proceed, [], "a");

    private static HttpAuthenticationVerdict Challenge(string[] challenges) => new(HttpAuthenticationOutcome.Challenge, challenges, null);

    private async Task AssertResetVersionFallbackUnjudgedAsync(string authorization)
    {
        var policy = new UnitTestAuthenticationPolicy(_ => Proceed());
        using var client = new Http3TestClient { ProtocolServerPolicy = policy };
        await client.StartAsync(Token);

        client.Send(0, client.Request("GET", "/file.txt", ("authorization", authorization)), fin: true);
        await client.UntilAsync(() => client.ResetCode(0) is not null && client.StopSendingCode(0) is not null, Token);
        client.Send(4, client.Request("GET", "/file.txt"), fin: true);
        await client.UntilAsync(() => client.HasEnded(4), Token);

        Assert.AreEqual(H3VersionFallback, client.ResetCode(0));
        Assert.AreEqual(H3VersionFallback, client.StopSendingCode(0));
        Assert.IsEmpty(client.StreamSent(0).Data, "No HEADERS went out before the reset.");
        Assert.AreEqual("200", client.Response(4).Status);
        Assert.HasCount(1, policy.JudgedRequests, "The authentication session never sees the NTLM or Negotiate stream, so no handshake starts.");
        CollectionAssert.Contains(client.Log.Notes.ToArray(), "Stream 0 was reset with VersionFallback: its authorization names NTLM or Negotiate, which HTTP/3 does not carry.");
        Assert.IsNull(client.CloseCode);
    }
}
