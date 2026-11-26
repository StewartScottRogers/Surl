using System.Buffers;
using System.Text;
using Surl.Kerberos;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

/// <summary>
/// What the exchange log holds for a client that sends ahead of the server's SOCKS replies
/// (ADR-0182, amendment 12). curl waits for each reply, so these bytes are not curl's.
/// </summary>
[TestClass]
public sealed class SocksSendAheadRedactionTests
{
    private const string Password = "s3cret-pw";
    private const string SecretLine = "PASS s3cret-pw\r\n";
    private const string MaskedLine = "PASS ****\r\n";

    private static readonly byte[] PasswordGreeting = [5, 1, 2];
    private static readonly byte[] NoAuthenticationGreeting = [5, 1, 0];
    private static readonly byte[] Login = [1, 3, .. "app"u8, 9, .. "s3cret-pw"u8];
    private static readonly byte[] MaskedLogin = [1, 3, .. "app"u8, 4, .. "****"u8];
    private static readonly byte[] DomainRequest = SocksTestKit.Socks5DomainRequest("localhost", 18021);
    private static readonly byte[] GssApiMessages = [1, 1, 0, 2, 0x60, 0x00, 1, 2, 0, 1, 0];

    private static IReceivedBytesRedactor NewRedactor(SocksGssApiLogin? gssApi = null) =>
        new SocksProtocolServer(new InMemoryOriginConnector(), null, gssApi)
            .CreateRedactor(new ExchangeRedactionContext(1, "socks5"));

    private static SocksGssApiLogin GssApiLogin()
    {
        var clock = new ManualTimeProvider(SocksTestKit.Now);

        return new SocksGssApiLogin(
            new KerberosAcceptor(
                ApRequestBuilder.Keytab("rcmd"), new KerberosReplayCache(clock), clock, new Socks5GssApiTests.ZeroRandomSource()),
            "rcmd");
    }

    private static byte[] Logged(params byte[][] chunks) => LoggedBy(NewRedactor(), chunks);

    private static byte[] LoggedWithGssApi(params byte[][] chunks) => LoggedBy(NewRedactor(GssApiLogin()), chunks);

    private static byte[] LoggedBy(IReceivedBytesRedactor redactor, byte[][] chunks)
    {
        var output = new ArrayBufferWriter<byte>();
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
        }

        redactor.Flush(output);

        return output.WrittenSpan.ToArray();
    }

    private static void AssertLoggedHoweverSplit(byte[] expected, byte[] sent, Func<byte[][], byte[]>? logged = null)
    {
        logged ??= Logged;
        for (var split = 0; split < sent.Length; split++)
        {
            CollectionAssert.AreEqual(expected, logged([sent[..split], sent[split..]]), $"split at {split}");
        }

        CollectionAssert.AreEqual(expected, logged([.. sent.Select(value => new[] { value })]));
    }

    private static byte[] Bytes(string text) => SocksTestKit.Latin1Bytes(text);

    [TestMethod]
    public async Task GreetingAndLoginInOneRead_BeforeTheMethodSelection_LogNoPassword()
    {
        var log = new RecordingExchangeLog();
        var server = new SocksProtocolServer(
            new InMemoryOriginConnector().ScriptConnection("localhost", 18021, SocksTestKit.ConnectionOf(Bytes("220 ready\r\n"))),
            new ScriptedProxyAccounts(refused: PasswordLoginVerdict.Accepted));
        var redactor = server.CreateRedactor(new ExchangeRedactionContext(1, "socks5"));
        var client = new RedactingLogConnection(
            SocksTestKit.ConnectionOf([.. PasswordGreeting, .. Login], DomainRequest), redactor, log);

        await server.ServeAsync(client, SocksTestKit.Context(log, scheme: "socks5"));
        client.FlushLog();

        var logged = log.Entries
            .Where(entry => entry.Kind == ExchangeLogEntryKind.BytesReceived)
            .SelectMany(entry => entry.Bytes.ToArray())
            .ToArray();
        CollectionAssert.AreEqual((byte[])[.. PasswordGreeting, .. MaskedLogin, .. DomainRequest], logged);
        Assert.IsFalse(Encoding.Latin1.GetString(logged).Contains(Password, StringComparison.Ordinal));
    }

    [TestMethod]
    public void GreetingLoginRequestAndTunnelledBytes_HoweverSplit_LogNoPassword() =>
        AssertLoggedHoweverSplit(
            [.. PasswordGreeting, .. MaskedLogin, .. DomainRequest, .. Bytes(MaskedLine)],
            [.. PasswordGreeting, .. Login, .. DomainRequest, .. Bytes(SecretLine)]);

    [TestMethod]
    public void LoginOfAnEmptyUserNameAndPassword_IsFollowedToTheRequest()
    {
        byte[] login = [1, 0, 0];

        AssertLoggedHoweverSplit(
            [.. PasswordGreeting, 1, 0, 4, .. "****"u8, .. DomainRequest, .. Bytes(MaskedLine)],
            [.. PasswordGreeting, .. login, .. DomainRequest, .. Bytes(SecretLine)]);
    }

    [TestMethod]
    public void LoginAheadFromAClientOfferingNoPasswordMethod_IsMaskedAll()
    {
        var logged = Logged([.. NoAuthenticationGreeting, .. Login]);

        CollectionAssert.AreEqual((byte[])[.. NoAuthenticationGreeting, .. MaskedLogin], logged);
    }

    [TestMethod]
    public void RepliesArrivingAfterTheBytesTheyAnswer_ChangeNothing()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact([.. PasswordGreeting, .. Login, .. DomainRequest, .. Bytes("PASS s3c")], output);
        redactor.ObserveSent([5, 2]);
        redactor.ObserveSent([1, 0]);
        redactor.ObserveSent(SocksTestKit.Socks5Succeeded);
        redactor.Redact(Bytes("ret-pw\r\n"), output);
        redactor.Flush(output);

        CollectionAssert.AreEqual(
            (byte[])[.. PasswordGreeting, .. MaskedLogin, .. DomainRequest, .. Bytes(MaskedLine)],
            output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void RepliesBeforeTheOriginsContinuation_AreNotReadAsPartOfIt()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact([.. NoAuthenticationGreeting, .. DomainRequest, .. Bytes("AUTH LOGIN\r\n")], output);
        redactor.ObserveSent([5, 0]);
        redactor.ObserveSent(SocksTestKit.Socks5Succeeded);
        redactor.ObserveSent("334 VXNlcm5hbWU6\r\n"u8);
        redactor.Redact(Bytes("YXBw\r\n"), output);
        redactor.Flush(output);

        CollectionAssert.AreEqual(
            (byte[])[.. NoAuthenticationGreeting, .. DomainRequest, .. Bytes("AUTH LOGIN\r\n****\r\n")],
            output.WrittenSpan.ToArray());
    }

    [TestMethod]
    [DataRow((byte)1, 4)]
    [DataRow((byte)3, 10)]
    [DataRow((byte)4, 16)]
    public void Socks5RequestAndPassInOneRead_LogThePasswordMasked(byte addressType, int addressLength)
    {
        byte[] address = addressType == 3 ? [9, .. "localhost"u8] : new byte[addressLength];
        byte[] request = SocksTestKit.Socks5Request(1, addressType, address, 18021);

        AssertLoggedHoweverSplit(
            [.. NoAuthenticationGreeting, .. request, .. Bytes(MaskedLine)],
            [.. NoAuthenticationGreeting, .. request, .. Bytes(SecretLine)]);
    }

    [TestMethod]
    public void Socks5RequestAndPassInOneRead_AfterTheMethodSelection_LogsThePasswordMasked()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(NoAuthenticationGreeting, output);
        redactor.ObserveSent([5, 0]);
        redactor.Redact([.. DomainRequest, .. Bytes(SecretLine)], output);
        redactor.Flush(output);

        CollectionAssert.AreEqual(
            (byte[])[.. NoAuthenticationGreeting, .. DomainRequest, .. Bytes(MaskedLine)],
            output.WrittenSpan.ToArray());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("app")]
    public void Socks4RequestAndPassInOneRead_LogThePasswordMasked(string userId)
    {
        byte[] request = SocksTestKit.Socks4Request(1, 18021, [127, 0, 0, 1], userId);

        AssertLoggedHoweverSplit([.. request, .. Bytes(MaskedLine)], [.. request, .. Bytes(SecretLine)]);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("app")]
    public void Socks4aRequestAndPassInOneRead_LogThePasswordMasked(string userId)
    {
        byte[] request = SocksTestKit.Socks4Request(1, 18021, [0, 0, 0, 1], userId, [.. "localhost"u8, 0]);

        AssertLoggedHoweverSplit([.. request, .. Bytes(MaskedLine)], [.. request, .. Bytes(SecretLine)]);
    }

    [TestMethod]
    public void Http2PrefaceSentWithTheRequest_IsFoundAtTheTunnelsStart()
    {
        // A SETTINGS frame and a HEADERS frame holding the literal field "authorization: x".
        byte[] settings = [0, 0, 0, 4, 0, 0, 0, 0, 0];
        byte[] block = [0x40, 13, .. "authorization"u8, 1, (byte)'x'];
        byte[] headersHead = [0, 0, (byte)block.Length, 1, 4, 0, 0, 0, 1];
        byte[] opening = [.. NoAuthenticationGreeting, .. DomainRequest, .. Bytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"), .. settings, .. headersHead];

        var logged = Logged([.. opening, .. block]);

        CollectionAssert.AreEqual((byte[])[.. opening, .. "****"u8], logged);
    }

    [TestMethod]
    [DataRow(new byte[] { 5, 1, 1 }, DisplayName = "offering GSS-API alone")]
    [DataRow(new byte[] { 5, 2, 1, 2 }, DisplayName = "offering GSS-API and a password")]
    public void GssApiMessagesAhead_OnAListenerOfferingGssApi_AreFollowedToTheTunnel(byte[] greeting) =>
        AssertLoggedHoweverSplit(
            [.. greeting, .. GssApiMessages, .. DomainRequest, .. Bytes(MaskedLine)],
            [.. greeting, .. GssApiMessages, .. DomainRequest, .. Bytes(SecretLine)],
            LoggedWithGssApi);

    [TestMethod]
    public void GssApiContextMessageWithAnEmptyToken_IsFollowedByTheNextMessage()
    {
        byte[] sent = [5, 1, 1, 1, 1, 0, 0, 1, 2, 0, 1, 0, .. DomainRequest];

        CollectionAssert.AreEqual(
            (byte[])[.. sent, .. Bytes(MaskedLine)], LoggedWithGssApi([.. sent, .. Bytes(SecretLine)]));
    }

    [TestMethod]
    public void GssApiMessageOfAnUnknownType_IsLoggedAsReceived()
    {
        byte[] sent = [5, 1, 1, 1, 0xFF, .. Bytes(SecretLine)];

        CollectionAssert.AreEqual(sent, LoggedWithGssApi(sent));
    }

    [TestMethod]
    public void LoginAhead_OnAListenerOfferingGssApiToAClientThatDoesNot_IsMasked()
    {
        var logged = LoggedWithGssApi([.. PasswordGreeting, .. Login]);

        CollectionAssert.AreEqual((byte[])[.. PasswordGreeting, .. MaskedLogin], logged);
    }

    [TestMethod]
    public void LoginAhead_OnAListenerWithoutGssApiToAClientOfferingIt_IsMasked()
    {
        byte[] greeting = [5, 1, 1];

        CollectionAssert.AreEqual((byte[])[.. greeting, .. MaskedLogin], Logged([.. greeting, .. Login]));
    }

    [TestMethod]
    [DataRow(new byte[] { 5, 0 }, DisplayName = "after the server selected no authentication")]
    [DataRow(new byte[] { 5, 2 }, DisplayName = "after the server selected a password")]
    public void LoginSentInASecondRead_IsMaskedWhateverTheServerSelected(byte[] selection)
    {
        byte[] greeting = [5, 2, 0, 2];
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(greeting, output);
        redactor.ObserveSent(selection);
        redactor.Redact(Login, output);

        CollectionAssert.AreEqual((byte[])[.. greeting, .. MaskedLogin], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void LoginOfAnotherVersionFromAClientOfferingAPassword_IsStillMasked()
    {
        var logged = Logged([.. PasswordGreeting, 9, .. Login.AsSpan(1)]);

        CollectionAssert.AreEqual((byte[])[.. PasswordGreeting, 9, .. MaskedLogin.AsSpan(1)], logged);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x16, 3, 1 }, DisplayName = "the first byte is no SOCKS version")]
    [DataRow(new byte[] { 5, 1, 0, 9 }, DisplayName = "the byte after the greeting is neither a login nor a request")]
    [DataRow(new byte[] { 5, 1, 0, 5, 1, 0, 2 }, DisplayName = "the address type is between the known ones")]
    [DataRow(new byte[] { 5, 1, 0, 5, 1, 0, 9 }, DisplayName = "the address type is past the known ones")]
    public void BytesNoSocksHandshakeExplains_AreLoggedAsReceived(byte[] handshake)
    {
        byte[] sent = [.. handshake, .. Bytes(SecretLine)];

        CollectionAssert.AreEqual(sent, Logged(sent));
    }

    [TestMethod]
    public void BytesThatAreNoRequestAfterTheLogin_AreLoggedAsReceived()
    {
        byte[] sent = [.. PasswordGreeting, .. Login, 9, .. Bytes(SecretLine)];

        CollectionAssert.AreEqual(
            (byte[])[.. PasswordGreeting, .. MaskedLogin, 9, .. Bytes(SecretLine)], Logged(sent));
    }

    [TestMethod]
    public void BytesNoSocksHandshakeExplains_ThenAGrantedRequest_StartTheTunnel()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact([5, 1, 0, 9], output);
        redactor.ObserveSent(SocksTestKit.Socks5Succeeded);
        redactor.Redact(Bytes(SecretLine), output);
        redactor.Flush(output);

        CollectionAssert.AreEqual((byte[])[5, 1, 0, 9, .. Bytes(MaskedLine)], output.WrittenSpan.ToArray());
    }
}
