using System.Net;
using Surl.Kerberos;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Socks.SocksTestKit;

namespace Surl.Protocol.Socks;

/// <summary>
/// RFC 1961 GSS-API authentication on the SOCKS5 server (ADR-0121 decision 3, BL-765): the
/// hand-made Kerberos AP-REQs of ADR-0057 decision 11 replayed through <see cref="InMemoryConnection"/>.
/// </summary>
[TestClass]
public sealed class Socks5GssApiTests
{
    private static readonly byte[] GreetingWithGssApi = [0x05, 0x02, 0x00, 0x01];

    private static readonly byte[] ConnectRequest = Socks5Request(0x01, 0x01, [127, 0, 0, 1], 8080);

    private static readonly byte[] Failure = [0x01, 0xFF];

    private readonly ManualTimeProvider clock = new(ApRequestBuilder.Now);

    private static ApRequestBuilder Ticket(bool mutualRequired = true) => new()
    {
        MutualRequired = mutualRequired,
        ServerName = ["rcmd", ApRequestBuilder.Host],
    };

    private static byte[] Message(byte type, byte[] token) => [0x01, type, (byte)(token.Length >> 8), (byte)token.Length, .. token];

    private static byte[] ProtectionMessage(ApRequestBuilder ticket, byte level = 0x01) =>
        Message(0x02, new InitiatorTokens(ticket.SessionKeyType, ticket.SessionKey).WrapSigned([level], (ulong)ticket.SequenceNumber!.Value));

    private SocksGssApiLogin Login(string service = "rcmd") =>
        new(new KerberosAcceptor(ApRequestBuilder.Keytab("rcmd"), new KerberosReplayCache(clock), clock, new ZeroRandomSource()), service);

    private async Task<(InMemoryConnection Client, InMemoryConnection Origin, RecordingExchangeLog Log)> ServeAsync(
        SocksGssApiLogin? gssApi, IAuthenticationPolicy? accounts, params byte[][] chunks)
    {
        var client = ConnectionOf(chunks);
        var origin = new InMemoryConnection(
            new ReadOnlyMemory<byte>[] { Latin1Bytes(OriginResponse) }, localEndPoint: new IPEndPoint(IPAddress.Loopback, 8080));
        var log = new RecordingExchangeLog();
        var origins = new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, origin);
        await new SocksProtocolServer(origins, accounts, gssApi).ServeAsync(client, Context(log, clock, scheme: "socks5"));

        return (client, origin, log);
    }

    [TestMethod]
    public async Task ServeAsync_KerberosTokenWithMutualAuthentication_AnswersApRepAndProtectionAndRelays()
    {
        var ticket = Ticket();

        var (client, origin, log) = await ServeAsync(
            Login(), null, GreetingWithGssApi, Message(0x01, ticket.Build()), ProtectionMessage(ticket), ConnectRequest, Latin1Bytes("GET / HTTP/1.1\r\n\r\n"));

        var written = client.WrittenBytes;
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x01 }, written[..2], "method 01 is selected");
        Assert.AreEqual(0x01, written[2]);
        Assert.AreEqual(0x01, written[3]);
        var apRepLength = (written[4] << 8) | written[5];
        Assert.AreEqual(0x60, written[6], "the AP-REP is a GSS-API token");
        var protection = 6 + apRepLength;
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02 }, written[protection..(protection + 2)]);
        var wrapLength = (written[protection + 2] << 8) | written[protection + 3];
        CollectionAssert.AreEqual(new byte[] { 0x05, 0x04, 0x01, 0xFF }, written[(protection + 4)..(protection + 8)], "an RFC 4121 wrap token sent by the acceptor");
        var rest = protection + 4 + wrapLength;
        CollectionAssert.AreEqual((byte[])[.. Socks5Succeeded, .. Latin1Bytes(OriginResponse)], written[rest..]);
        CollectionAssert.AreEqual(Latin1Bytes("GET / HTTP/1.1\r\n\r\n"), origin.WrittenBytes, "the relay carries curl's bytes unwrapped.");
        Assert.Contains("SOCKS5 GSS-API login: user@EXAMPLE.COM; protection none.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_KerberosTokenWithoutMutualAuthenticationAndRawLevel_SendsNoApRepAndAnswersRawLevel()
    {
        var ticket = Ticket(mutualRequired: false);

        var (client, origin, _) = await ServeAsync(
            Login(), null, GreetingWithGssApi, Message(0x01, ticket.Build()), Message(0x02, [0x01]), ConnectRequest);

        byte[] expected = [0x05, 0x01, 0x01, 0x02, 0x00, 0x01, 0x00, .. Socks5Succeeded, .. Latin1Bytes(OriginResponse)];
        CollectionAssert.AreEqual(expected, client.WrittenBytes);
        Assert.IsTrue(origin.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_KerberosTokenWithBadTicket_AnswersFailureAndCloses()
    {
        var (client, origin, log) = await ServeAsync(
            Login(), null, GreetingWithGssApi, Message(0x01, (Ticket() with { TamperTicket = true }).Build()), ConnectRequest);

        CollectionAssert.AreEqual((byte[])[0x05, 0x01, .. Failure], client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
        Assert.IsEmpty(origin.WrittenBytes);
        Assert.Contains("Answered 01 FF: the Kerberos token is refused: integrity check failed.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_TicketForAnotherService_AnswersFailure()
    {
        var (client, _, _) = await ServeAsync(Login("rcmd"), null, GreetingWithGssApi, Message(0x01, new ApRequestBuilder().Build()));

        CollectionAssert.AreEqual((byte[])[0x05, 0x01, .. Failure], client.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_ServiceNameFromTheLogin_IsTheOneTicketsMustNameWhenItIsNotRcmd()
    {
        var ticket = Ticket(mutualRequired: false) with { ServerName = ["host", ApRequestBuilder.Host] };
        var keytab = ApRequestBuilder.Keytab("host");
        var login = new SocksGssApiLogin(new KerberosAcceptor(keytab, new KerberosReplayCache(clock), clock, new ZeroRandomSource()), "host");

        var (client, _, _) = await ServeAsync(login, null, GreetingWithGssApi, Message(0x01, ticket.Build()), Message(0x02, [0x01]), ConnectRequest);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0x01, 0x01, 0x02, 0x00, 0x01, 0x00 }, client.WrittenBytes[..7]);
    }

    [TestMethod]
    [DataRow(new byte[] { }, DisplayName = "no message")]
    [DataRow(new byte[] { 0x02, 0x01, 0x00, 0x01, 0x60 }, DisplayName = "wrong version")]
    [DataRow(new byte[] { 0x01, 0x02, 0x00, 0x01, 0x60 }, DisplayName = "wrong message type")]
    [DataRow(new byte[] { 0x01, 0x01, 0x00, 0x00 }, DisplayName = "zero length")]
    [DataRow(new byte[] { 0x01, 0x01, 0x00, 0x05, 0x60 }, DisplayName = "truncated token")]
    public async Task ServeAsync_MalformedContextMessage_AnswersFailure(byte[] message)
    {
        var (client, _, _) = await ServeAsync(Login(), null, GreetingWithGssApi, message);

        CollectionAssert.AreEqual((byte[])[0x05, 0x01, .. Failure], client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
    }

    [TestMethod]
    public async Task ServeAsync_ProtectionMessageMissing_AnswersFailureAfterApRep()
    {
        var (client, origin, log) = await ServeAsync(Login(), null, GreetingWithGssApi, Message(0x01, Ticket().Build()));

        Assert.AreEqual(0x01, client.WrittenBytes[2]);
        CollectionAssert.AreEqual(Failure, client.WrittenBytes[^2..]);
        Assert.IsEmpty(origin.WrittenBytes);
        Assert.Contains("Answered 01 FF: the GSS-API protection message is missing or malformed.", log.Notes);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "not a wrap token")]
    [DataRow(true, DisplayName = "a wrap token of two bytes")]
    public async Task ServeAsync_ProtectionMessageThatIsNotOneWrappedByte_AnswersFailure(bool isWrapToken)
    {
        var ticket = Ticket(mutualRequired: false);
        var protection = isWrapToken
            ? Message(0x02, new InitiatorTokens(ticket.SessionKeyType, ticket.SessionKey).WrapSigned([0x01, 0x02], (ulong)ticket.SequenceNumber!.Value))
            : Message(0x02, new byte[20]);

        var (client, _, _) = await ServeAsync(Login(), null, GreetingWithGssApi, Message(0x01, ticket.Build()), protection, ConnectRequest);

        CollectionAssert.AreEqual((byte[])[0x05, 0x01, .. Failure], client.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_NoKeytab_NeverSelectsGssApi()
    {
        var (client, _, _) = await ServeAsync(null, null, GreetingWithGssApi, ConnectRequest);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0x00 }, client.WrittenBytes[..2]);
    }

    [TestMethod]
    public async Task ServeAsync_NoKeytabAndOnlyGssApiOffered_AnswersNoAcceptableMethod()
    {
        var (client, _, _) = await ServeAsync(null, null, [0x05, 0x01, 0x01]);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0xFF }, client.WrittenBytes[..2]);
    }

    [TestMethod]
    public async Task ServeAsync_KeytabButGssApiNotOffered_UsesTheOtherMethods()
    {
        var (client, _, _) = await ServeAsync(Login(), null, [0x05, 0x01, 0x00], ConnectRequest);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0x00 }, client.WrittenBytes[..2]);
    }

    [TestMethod]
    public void Constructor_NullAcceptorOrService_Throws()
    {
        var acceptor = Login().Acceptor;

        Assert.ThrowsExactly<ArgumentNullException>(() => new SocksGssApiLogin(null!, "rcmd"));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SocksGssApiLogin(acceptor, null!));
        Assert.AreEqual("rcmd", SocksGssApiLogin.DefaultService);
    }

    private sealed class ZeroRandomSource : IKerberosRandomSource
    {
        public void Fill(Span<byte> destination) => destination.Clear();
    }
}
