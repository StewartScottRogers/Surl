using System.Security.Cryptography;
using System.Text;
using Surl.Authentication;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Azure Storage server's login session starts with its exchange, so a login is published and
/// refused under the listen scheme (<c>az</c>, <c>abfs</c> or <c>abfss</c>) and not as <c>http</c>
/// (BL-1208; ADR-0124, decisions 1 and 4).
/// </summary>
[TestClass]
public sealed class AzureLoginExchangeTests
{
    private const string AccountName = "account";

    private static readonly byte[] Key = Encoding.ASCII.GetBytes("0123456789abcdef0123456789abcdef");

    [TestMethod]
    [DataRow("az")]
    [DataRow("abfs")]
    public async Task ServeAsync_AnyListenScheme_StartsTheSessionWithThatSchemeAndTheContextsExchangeId(string scheme)
    {
        var harness = new AzureServerHarness { Scheme = scheme };
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");

        await harness.SendWithBearerAsync("GET", "/photos/a.txt");

        var exchange = harness.Policy.StartedExchanges.Single();
        Assert.IsNotNull(exchange);
        Assert.AreEqual(scheme, exchange.Scheme);
        Assert.AreEqual(harness.Context().ExchangeId, exchange.ExchangeId);
    }

    [TestMethod]
    public async Task ServeAsync_CorrectSharedKeyUnderRefuseLoginForTheScheme_IsRefusedAsAWrongKeyIsAndPublishedUnderTheScheme()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");
        var server = ServerWith(harness, new FaultRule("f1", FaultKind.RefuseLogin) { Scheme = "az" });

        var refused = await SendSharedKeyAsync(harness, server, Sign("GET\n\n\n\n\n\n\n\n\n\n\n\n" + SignedFields));
        var wrong = await SendSharedKeyAsync(harness, ServerWith(harness), Sign("wrong"));

        Assert.StartsWith("HTTP/1.1 403 ", refused);
        Assert.Contains("x-ms-error-code: AuthenticationFailed\r\n", refused);
        Assert.AreEqual(StatusAndCode(wrong), StatusAndCode(refused));
        var judged = harness.Events.Events.OfType<LoginJudgedEvent>().First();
        Assert.AreEqual(new LoginJudgedEvent(harness.Context().ExchangeId, "az", AccountName, "SharedKey", false, true), judged);
    }

    [TestMethod]
    public async Task ServeAsync_CorrectSharedKeyUnderRefuseLoginForHttp_IsServed()
    {
        var harness = new AzureServerHarness();
        harness.CreateFolder("photos");
        harness.WriteFile("photos/a.txt", "hello world");
        var server = ServerWith(harness, new FaultRule("f1", FaultKind.RefuseLogin) { Scheme = "http" });

        var response = await SendSharedKeyAsync(harness, server, Sign("GET\n\n\n\n\n\n\n\n\n\n\n\n" + SignedFields));

        Assert.StartsWith("HTTP/1.1 200 ", response);
        Assert.IsFalse(harness.Events.Events.OfType<LoginJudgedEvent>().Any(judged => judged.Scheme == "http"));
    }

    private static string SignedFields =>
        $"x-ms-date:{AzureServerHarness.NowHttpDate}\nx-ms-version:2025-01-05\n/{AccountName}/photos/a.txt";

    private static string Sign(string stringToSign) =>
        Convert.ToBase64String(HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(stringToSign)));

    private static string StatusAndCode(string response)
    {
        var head = response[..response.IndexOf("\r\n\r\n", StringComparison.Ordinal)];

        return head.Split("\r\n").First() + "|" + head.Split("\r\n").First(line => line.StartsWith("x-ms-error-code:", StringComparison.Ordinal));
    }

    private static AzureStorageProtocolServer ServerWith(AzureServerHarness harness, params FaultRule[] rules)
    {
        var settings = new AuthenticationSettings(
            new AccountBook([new Account(AccountName, Convert.ToBase64String(Key))]),
            false,
            false,
            new HashSet<AuthenticationMethod> { AuthenticationMethod.Basic });
        var policy = new AuthenticationPolicy(settings, [], harness.TimeProvider, harness.Events, new ScriptedFaultPlan(rules));

        return new AzureStorageProtocolServer(harness.Store, harness.Index, policy, harness.TimeProvider);
    }

    // Serves the request while moving the fixed clock past the refusal delay, which a refusal waits for.
    private static async Task<string> SendSharedKeyAsync(AzureServerHarness harness, AzureStorageProtocolServer server, string signature)
    {
        var request =
            $"GET /photos/a.txt HTTP/1.1\r\nHost: {AccountName}.blob.core.windows.net\r\nx-ms-date: {AzureServerHarness.NowHttpDate}\r\n"
            + $"x-ms-version: 2025-01-05\r\nAuthorization: SharedKey {AccountName}:{signature}\r\nConnection: close\r\n\r\n";
        var connection = new InMemoryConnection([Encoding.Latin1.GetBytes(request)]);

        var serving = server.ServeAsync(connection, harness.Context());
        while (!serving.IsCompleted)
        {
            harness.TimeProvider.Advance(AuthenticationPolicy.RefusalDelay);
            await Task.Yield();
        }

        await serving;

        return Encoding.UTF8.GetString(connection.WrittenBytes);
    }
}
