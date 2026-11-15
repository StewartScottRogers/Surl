using System.Text;
using Surl.Authentication;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The Cloud Storage server starts its HTTP login session with its exchange, so a login is judged,
/// published and refused as <c>gs</c> (BL-1209).
/// </summary>
[TestClass]
public sealed class GcsLoginExchangeTests
{
    [TestMethod]
    public async Task ServeAsync_StartsTheSessionWithTheGsExchangeOfItsContext()
    {
        var harness = new GcsServerHarness();

        await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        var exchange = harness.Policy.StartedExchanges.Single();
        Assert.AreEqual("gs", exchange!.Scheme);
        Assert.AreEqual(harness.Context().ExchangeId, exchange.ExchangeId);
    }

    [TestMethod]
    [DataRow("gs", true)]
    [DataRow("http", false)]
    public async Task ServeAsync_RefuseLoginFault_RefusesAnAcceptedBearerTokenOnlyWhenScopedToGs(string scheme, bool isRefused)
    {
        var accounts = new AccountBook([new Account(string.Empty, "secret-token")]);
        var events = new RecordingSurlEventPublisher();
        var policy = new AuthenticationPolicy(
            new AuthenticationSettings(accounts, AllowAnonymous: false, AllowPlaintextAuthentication: true, new HashSet<AuthenticationMethod> { AuthenticationMethod.Bearer }),
            [new BearerAuthenticationMethod(accounts)],
            TimeProvider.System,
            events,
            new ScriptedFaultPlan(new FaultRule("f1", FaultKind.RefuseLogin) { Scheme = scheme }));
        var harness = new GcsServerHarness();
        var server = new GoogleCloudStorageProtocolServer(harness.Store, harness.Index, policy, harness.TimeProvider);
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes($"GET /photos/a.txt HTTP/1.1\r\nHost: 127.0.0.1:4443\r\n{GcsServerHarness.BearerField}\r\nConnection: close\r\n\r\n")]);

        await server.ServeAsync(connection, harness.Context());

        var login = events.Events.OfType<LoginJudgedEvent>().Single();
        Assert.AreEqual("gs", login.Scheme);
        Assert.AreEqual(1L, login.ExchangeId);
        Assert.AreEqual(isRefused, login.IsRefusedByFault);
        Assert.AreEqual(!isRefused, login.IsAccepted);
    }
}
