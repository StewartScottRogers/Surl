using System.Net;
using Surl.Kerberos;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Socks.SocksTestKit;

namespace Surl.Protocol.Socks;

/// <summary>
/// <see cref="Socks5GssApiNegotiation"/> asking the policy to judge the Kerberos login (ADR-0165, decisions 7 to 9).
/// </summary>
[TestClass]
public sealed class Socks5GssApiLoginJudgementTests
{
    private static readonly byte[] GreetingWithGssApi = [0x05, 0x02, 0x00, 0x01];

    private static readonly byte[] ConnectRequest = Socks5Request(0x01, 0x01, [127, 0, 0, 1], 8080);

    private readonly ManualTimeProvider clock = new(ApRequestBuilder.Now);

    private sealed class JudgingPolicy(bool verdict) : IAuthenticationPolicy
    {
        public List<KerberosLogin> Judged { get; } = [];

        public ValueTask<PasswordLoginVerdict> CheckPasswordLoginAsync(PasswordLogin login, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public IHttpAuthenticationSession StartHttpConnection(TlsSession? tlsSession) => throw new NotSupportedException();

        public ValueTask<bool> JudgeKerberosLoginAsync(KerberosLogin login, CancellationToken cancellationToken)
        {
            Judged.Add(login);

            return ValueTask.FromResult(verdict);
        }
    }

    private static byte[] Message(byte type, byte[] token) => [0x01, type, (byte)(token.Length >> 8), (byte)token.Length, .. token];

    private async Task<(InMemoryConnection Client, RecordingExchangeLog Log)> ServeAsync(IAuthenticationPolicy policy, params byte[][] chunks)
    {
        var login = new SocksGssApiLogin(
            new KerberosAcceptor(ApRequestBuilder.Keytab("rcmd"), new KerberosReplayCache(clock), clock, new Socks5GssApiTests.ZeroRandomSource()), "rcmd");
        var client = ConnectionOf(chunks);
        var origin = new InMemoryConnection(
            new ReadOnlyMemory<byte>[] { Latin1Bytes(OriginResponse) }, localEndPoint: new IPEndPoint(IPAddress.Loopback, 8080));
        var log = new RecordingExchangeLog();
        var origins = new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, origin);
        await new SocksProtocolServer(origins, policy, login).ServeAsync(client, Context(log, clock, scheme: "socks5"));

        return (client, log);
    }

    [TestMethod]
    public async Task ServeAsync_PolicyRefusesAcceptedLogin_AnswersFailureBeforeAnyApRep()
    {
        var policy = new JudgingPolicy(false);
        var ticket = new ApRequestBuilder { MutualRequired = true, ServerName = ["rcmd", ApRequestBuilder.Host] };

        var (client, log) = await ServeAsync(policy, GreetingWithGssApi, Message(0x01, ticket.Build()), ConnectRequest);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0x01, 0x01, 0xFF }, client.WrittenBytes);
        Assert.HasCount(1, policy.Judged);
        Assert.IsTrue(policy.Judged[0].IsTokenAccepted);
        Assert.AreEqual("user@EXAMPLE.COM", policy.Judged[0].ClientPrincipal);
        Assert.AreEqual("socks5", policy.Judged[0].Scheme);
        Assert.IsNotNull(policy.Judged[0].Exchange);
        Assert.Contains("SOCKS5 GSS-API login refused: user@EXAMPLE.COM.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_PolicyAcceptsLogin_ContinuesToProtectionLevel()
    {
        var policy = new JudgingPolicy(true);
        var ticket = new ApRequestBuilder { MutualRequired = false, ServerName = ["rcmd", ApRequestBuilder.Host] };

        var (client, _) = await ServeAsync(policy, GreetingWithGssApi, Message(0x01, ticket.Build()), Message(0x02, [0x01]), ConnectRequest);

        Assert.HasCount(1, policy.Judged);
        Assert.AreEqual(0x02, client.WrittenBytes[3]);
    }

    [TestMethod]
    public async Task ServeAsync_RefusedToken_IsJudgedWithoutAPrincipal()
    {
        var policy = new JudgingPolicy(false);

        var (client, log) = await ServeAsync(policy, GreetingWithGssApi, Message(0x01, new ApRequestBuilder().Build()));

        CollectionAssert.AreEqual(new byte[] { 0x01, 0xFF }, client.WrittenBytes[^2..]);
        Assert.HasCount(1, policy.Judged);
        Assert.IsFalse(policy.Judged[0].IsTokenAccepted);
        Assert.IsNull(policy.Judged[0].ClientPrincipal);
        Assert.IsFalse(log.Notes.Any(note => note.Contains("login refused")));
    }
}
