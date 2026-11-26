using System.Buffers;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Socks;

[TestClass]
public sealed class SocksExchangeLogRedactionTests
{
    private static readonly byte[] Greeting = [5, 1, 2];
    private static readonly byte[] PasswordMethod = [5, 2];
    private static readonly byte[] Login = [1, 5, .. "alice"u8, 6, .. "s3cret"u8];

    private static IReceivedBytesRedactor NewRedactor() =>
        new SocksProtocolServer(new InMemoryOriginConnector()).CreateRedactor(new ExchangeRedactionContext(1, "socks5"));

    private static byte[] Logged(IEnumerable<(byte[] Sent, byte[][] Received)> turns)
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var (sent, received) in turns)
        {
            redactor.ObserveSent(sent);
            foreach (var chunk in received)
            {
                redactor.Redact(chunk, output);
            }
        }

        redactor.Flush(output);

        return output.WrittenSpan.ToArray();
    }

    private static byte[] Masked() => [1, 5, .. "alice"u8, 4, .. "****"u8];

    [TestMethod]
    public void Socks5Subnegotiation_PasswordsOfDifferentLengths_AreLoggedTheSame()
    {
        byte[] Loginwith(int length) => [1, 5, .. "alice"u8, (byte)length, .. new byte[length].Select(_ => (byte)'x')];

        var shortLogged = Logged([([], [Greeting]), (PasswordMethod, [Loginwith(1)])]);
        var longLogged = Logged([([], [Greeting]), (PasswordMethod, [Loginwith(255)])]);

        byte[] expectedTail = [4, 0x2A, 0x2A, 0x2A, 0x2A];
        CollectionAssert.AreEqual(expectedTail, shortLogged[^5..]);
        CollectionAssert.AreEqual(shortLogged, longLogged);
    }

    [TestMethod]
    public void Socks5Subnegotiation_IsLoggedWithPasswdMasked()
    {
        var logged = Logged([([], [Greeting]), (PasswordMethod, [Login])]);

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. Masked()], logged);
    }

    [TestMethod]
    public void Socks5Subnegotiation_SplitAtEveryByte_IsLoggedTheSame()
    {
        for (var split = 1; split < Login.Length; split++)
        {
            var redactor = NewRedactor();
            var output = new ArrayBufferWriter<byte>();
            redactor.Redact(Greeting, output);
            redactor.ObserveSent(PasswordMethod);
            redactor.Redact(Login.AsSpan(0, split), output);
            redactor.Redact(Login.AsSpan(split), output);
            redactor.Flush(output);

            CollectionAssert.AreEqual((byte[])[.. Greeting, .. Masked()], output.WrittenSpan.ToArray(), $"split {split}");
        }
    }

    [TestMethod]
    public void Socks5Subnegotiation_OneByteAtATime_IsLoggedTheSame()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        foreach (var value in Greeting)
        {
            redactor.Redact([value], output);
        }

        redactor.ObserveSent(PasswordMethod);
        foreach (var value in Login)
        {
            redactor.Redact([value], output);
        }

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. Masked()], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void TunnelledBytesAfterTheReply_ContainingThePassword_AreLoggedUnchanged()
    {
        byte[] request = [5, 1, 0, 3, 1, (byte)'h', 0, 80];
        byte[] tunnelled = [.. "s3cret"u8];
        var logged = Logged([([], [Greeting]), (PasswordMethod, [Login]), ([1, 0], [request]), ([5, 0, 0, 1, 0, 0, 0, 0, 0, 0], [tunnelled])]);

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. Masked(), .. request, .. tunnelled], logged);
    }

    [TestMethod]
    public void Socks4Request_IsLoggedAsBefore()
    {
        byte[] request = [4, 1, 0, 80, 127, 0, 0, 1, .. "s3cret"u8, 0];

        CollectionAssert.AreEqual(request, Logged([([], [request])]));
    }

    [TestMethod]
    [DataRow(new byte[] { 5, 0 }, DisplayName = "no authentication")]
    [DataRow(new byte[] { 5, 0xFF }, DisplayName = "a refusal")]
    [DataRow(new byte[] { 5 }, DisplayName = "a short selection")]
    public void LoginSentWhateverTheServerSelected_IsLoggedWithPasswdMasked(byte[] selection)
    {
        byte[] greeting = [5, 1, 0];

        var logged = Logged([([], [greeting]), (selection, [Login])]);

        CollectionAssert.AreEqual((byte[])[.. greeting, .. Masked()], logged);
    }

    [TestMethod]
    public void GreetingOfNoMethods_ThenSentBytes_AreFollowedWithoutMasking()
    {
        byte[] greeting = [5, 0];
        var logged = Logged([([], [greeting]), ([5, 0xFF], [[9]])]);

        CollectionAssert.AreEqual((byte[])[5, 0, 9], logged);
    }

    [TestMethod]
    public void EmptyPassword_IsLoggedAsFourAndTheMask()
    {
        byte[] login = [1, 0, 0];

        CollectionAssert.AreEqual((byte[])[.. Greeting, 1, 0, 4, .. "****"u8], Logged([([], [Greeting]), (PasswordMethod, [login])]));
    }

    [TestMethod]
    public void SentBytesAfterTheMethodWasChosen_AreIgnored()
    {
        var redactor = NewRedactor();
        var output = new ArrayBufferWriter<byte>();
        redactor.Redact(Greeting, output);
        redactor.ObserveSent(PasswordMethod);
        redactor.ObserveSent([1, 0]);
        redactor.Redact(Login, output);

        CollectionAssert.AreEqual((byte[])[.. Greeting, .. Masked()], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void NullOutput_IsRefused()
    {
        var redactor = NewRedactor();

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([1], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }

    [TestMethod]
    public void NullOutput_InATunnel_IsRefused()
    {
        var redactor = NewRedactor();
        redactor.ObserveSent(SocksTestKit.Socks5Succeeded);

        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Redact([1], null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => redactor.Flush(null!));
    }

    private const string SecretLines = "PASS s3cret-pw\r\nAuthorization: Basic YXBwOnMzY3JldC1wdw==\r\n";
    private const string MaskedLines = "PASS ****\r\nAuthorization: Basic ****\r\n";

    private static readonly byte[] ConnectRequest = SocksTestKit.Socks5DomainRequest("localhost", 18021);

    private static IReceivedBytesRedactor GrantedSocks5Tunnel(bool withLogin, ArrayBufferWriter<byte> output)
    {
        var redactor = NewRedactor();
        redactor.Redact(withLogin ? Greeting : [5, 1, 0], output);
        redactor.ObserveSent(withLogin ? PasswordMethod : [5, 0]);
        if (withLogin)
        {
            redactor.Redact(Login, output);
            redactor.ObserveSent([1, 0]);
        }

        redactor.Redact(ConnectRequest, output);
        redactor.ObserveSent(SocksTestKit.Socks5Succeeded);
        output.Clear();

        return redactor;
    }

    private static IReceivedBytesRedactor GrantedSocks4Tunnel(ArrayBufferWriter<byte> output)
    {
        var redactor = NewRedactor();
        redactor.Redact(SocksTestKit.Socks4Request(1, 80, [127, 0, 0, 1], "app"), output);
        redactor.ObserveSent(SocksTestKit.Granted);
        output.Clear();

        return redactor;
    }

    private static string LoggedTunnel(params byte[][] chunks)
    {
        var output = new ArrayBufferWriter<byte>();
        var redactor = GrantedSocks5Tunnel(withLogin: false, output);
        foreach (var chunk in chunks)
        {
            redactor.Redact(chunk, output);
        }

        redactor.Flush(output);

        return Encoding.Latin1.GetString(output.WrittenSpan);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Socks5Tunnel_PassAndAuthorization_AreLoggedMasked(bool withLogin)
    {
        var output = new ArrayBufferWriter<byte>();
        var redactor = GrantedSocks5Tunnel(withLogin, output);

        redactor.Redact(SocksTestKit.Latin1Bytes(SecretLines), output);
        redactor.Flush(output);

        Assert.AreEqual(MaskedLines, Encoding.Latin1.GetString(output.WrittenSpan));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Socks5Tunnel_SplitAtEveryByte_IsLoggedMasked(bool withLogin)
    {
        var tunnelled = SocksTestKit.Latin1Bytes(SecretLines);
        for (var split = 1; split < tunnelled.Length; split++)
        {
            var output = new ArrayBufferWriter<byte>();
            var redactor = GrantedSocks5Tunnel(withLogin, output);
            redactor.Redact(tunnelled.AsSpan(0, split), output);
            redactor.ObserveSent("331 Password required\r\n"u8);
            redactor.Redact(tunnelled.AsSpan(split), output);
            redactor.Flush(output);

            Assert.AreEqual(MaskedLines, Encoding.Latin1.GetString(output.WrittenSpan), $"split {split}");
        }
    }

    [TestMethod]
    public void Socks5Tunnel_OneByteAtATimeWithEmptyReads_IsLoggedMasked()
    {
        var chunks = SocksTestKit.Latin1Bytes(SecretLines).SelectMany(value => new byte[][] { [], [value] });

        Assert.AreEqual(MaskedLines, LoggedTunnel([.. chunks]));
    }

    [TestMethod]
    public void Socks4Tunnel_PassAndAuthorization_AreLoggedMasked()
    {
        var output = new ArrayBufferWriter<byte>();
        var redactor = GrantedSocks4Tunnel(output);

        redactor.Redact(SocksTestKit.Latin1Bytes(SecretLines), output);
        redactor.Flush(output);

        Assert.AreEqual(MaskedLines, Encoding.Latin1.GetString(output.WrittenSpan));
    }

    [TestMethod]
    [DataRow("ACCT s3cret-pw\r\n", "ACCT ****\r\n")]
    [DataRow("acct s3cret-pw\r\n", "acct ****\r\n")]
    [DataRow("pass two words\r\n", "pass ****\r\n")]
    [DataRow("Proxy-Authorization: Basic c2VjcmV0\r\n", "Proxy-Authorization: Basic ****\r\n")]
    [DataRow("proxy-authorization:Bearer  c2VjcmV0\r\n", "proxy-authorization:Bearer  ****\r\n")]
    [DataRow("AUTHORIZATION : c2VjcmV0\r\n", "AUTHORIZATION : ****\r\n")]
    [DataRow("Authorization: c2VjcmV0 \r\n", "Authorization: ****\r\n")]
    public void Tunnel_EachSecretLine_IsLoggedMasked(string sent, string logged) =>
        Assert.AreEqual("GET / HTTP/1.1\r\n" + logged, LoggedTunnel(SocksTestKit.Latin1Bytes("GET / HTTP/1.1\r\n" + sent)));

    [TestMethod]
    [DataRow("USER app\r\n")]
    [DataRow("GET /hello.txt HTTP/1.1\r\n")]
    [DataRow("GET http://localhost:18080/hello.txt HTTP/1.1\r\n")]
    [DataRow("Host: localhost:18080\r\n")]
    [DataRow("PASS\r\n")]
    [DataRow("PASS \r\n")]
    [DataRow("PASS\t\r\n")]
    [DataRow("PASSIVE s3cret\r\n")]
    [DataRow("PASSIVE\ts3cret\r\n")]
    [DataRow("Authorization:\r\n")]
    [DataRow("Authorization: \r\n")]
    [DataRow("X-Authorization: value\r\n")]
    [DataRow("Accept: */*\r\n continued\r\n\r\n")]
    [DataRow("USER app")]
    public void Tunnel_ALineWithNoSecret_IsLoggedAsReceived(string sent) =>
        Assert.AreEqual(sent, LoggedTunnel(SocksTestKit.Latin1Bytes(sent)));

    [TestMethod]
    public void Tunnel_FoldedAuthorization_HasEveryContinuationMasked()
    {
        var sent = "GET / HTTP/1.1\r\nAuthorization:\r\n Basic\r\n\tc2VjcmV0\r\n\r\n continued\r\n";

        Assert.AreEqual(
            "GET / HTTP/1.1\r\nAuthorization:\r\n ****\r\n\t****\r\n\r\n continued\r\n",
            LoggedTunnel(SocksTestKit.Latin1Bytes(sent)));
    }

    [TestMethod]
    public void TunnelledHttp1Request_ApiKeyFields_AreLoggedAsMasks()
    {
        foreach (var name in HttpWithheldHeaderNames.ApiKeysAndSessionTokens)
        {
            // Two words, so a rule that kept a scheme word would show; one line ends with a bare line feed.
            var sent = $"GET / HTTP/1.1\r\n{name}: Key s3cret\r\n \tfolded-s3cret\r\n"
                + $"{name.ToUpperInvariant()} :s3cret\n\tfolded-s3cret\n"
                + $"{name.ToLowerInvariant()}:\r\n s3cret\r\n"
                + "X-Other: shown\r\n continued\r\n\r\n";
            var expected = $"GET / HTTP/1.1\r\n{name}: ****\r\n \t****\r\n"
                + $"{name.ToUpperInvariant()} :****\n\t****\n"
                + $"{name.ToLowerInvariant()}:\r\n ****\r\n"
                + "X-Other: shown\r\n continued\r\n\r\n";

            SocksTunnelRedactionTestKit.AssertLoggedHoweverSplit(
                SocksTestKit.Latin1Bytes(expected),
                SocksTestKit.Latin1Bytes(sent));
        }
    }

    [TestMethod]
    [DataRow("X-Api-Key:\r\n")]
    [DataRow("X-Api-Key-Id: shown\r\n")]
    [DataRow("My-Api-Key: shown\r\n")]
    public void Tunnel_AHeaderThatOnlyResemblesAnApiKeyField_IsLoggedAsReceived(string sent) =>
        CollectionAssert.AreEqual(
            SocksTestKit.Latin1Bytes(sent),
            SocksTunnelRedactionTestKit.Logged(SocksTestKit.Latin1Bytes(sent)));

    [TestMethod]
    [DataRow(" X-Api-Key: s", " X-Api-Key: ****")]
    [DataRow("\tX-Api-Key: s", "\tX-Api-Key: ****")]
    [DataRow(" Authorization: Basic s", " Authorization: Basic ****")]
    [DataRow("\t Proxy-Authorization: s", "\t Proxy-Authorization: ****")]
    public void Tunnel_ASecretHeaderStartingWithASpaceOrATab_IsLoggedMasked(string sent, string logged) =>
        SocksTunnelRedactionTestKit.AssertLoggedHoweverSplit(
            SocksTestKit.Latin1Bytes($"GET / HTTP/1.1\r\n{logged}\r\n"),
            SocksTestKit.Latin1Bytes($"GET / HTTP/1.1\r\n{sent}\r\n"));

    [TestMethod]
    [DataRow("X-Other: a\rX-Api-Key: s\r\n", "X-Other: a\rX-Api-Key: ****\r\n")]
    [DataRow("X-Other: a\r Authorization: Basic s\r\n", "X-Other: a\r Authorization: Basic ****\r\n")]
    [DataRow("Authorization: s\rX-Other: a b\r\n", "Authorization: ****\r\n")]
    [DataRow("X-Other: a\rX-Api-Key: s\r\n s\r\n", "X-Other: a\rX-Api-Key: ****\r\n ****\r\n")]
    [DataRow("a AUTH PLAIN b\rX-Api-Key: s\r\n", "a AUTH PLAIN ****\r\n")]
    [DataRow("AUTHENTICATE PLAIN b\rX-Api-Key: s\r\n", "AUTHENTICATE PLAIN ****\r\n")]
    public void Tunnel_ASecretHeaderAroundABareCarriageReturn_IsLoggedMasked(string sent, string logged) =>
        SocksTunnelRedactionTestKit.AssertLoggedHoweverSplit(
            SocksTestKit.Latin1Bytes(logged),
            SocksTestKit.Latin1Bytes(sent));

    [TestMethod]
    public void Tunnel_LinesEndedByABareLineFeed_AreEachJudged()
    {
        var logged = LoggedTunnel(SocksTestKit.Latin1Bytes("USER app\nPASS s3cret-pw\nQUIT\n"));

        Assert.AreEqual("USER app\nPASS ****\nQUIT\n", logged);
    }

    [TestMethod]
    public void Tunnel_PassSeparatedByATab_IsMaskedKeepingTheTab() =>
        CollectionAssert.AreEqual(
            SocksTestKit.Latin1Bytes("USER app\r\nPASS\t****\r\npass\t****\nQUIT\r\n"),
            SocksTunnelRedactionTestKit.Logged(
                SocksTestKit.Latin1Bytes("USER app\r\nPASS\ts3cret-pw\r\npass\t\ts3cret pw\nQUIT\r\n")));

    [TestMethod]
    public void Tunnel_AcctSeparatedByATab_IsMaskedKeepingTheTab() =>
        CollectionAssert.AreEqual(
            SocksTestKit.Latin1Bytes("USER app\r\nACCT\t****\r\nacct\t****\nQUIT\r\n"),
            SocksTunnelRedactionTestKit.Logged(
                SocksTestKit.Latin1Bytes("USER app\r\nACCT\ts3cret-account\r\nacct\t s3cret account\nQUIT\r\n")));

    [TestMethod]
    public void Tunnel_AnUnfinishedPassLine_IsMaskedAtTheEnd() =>
        Assert.AreEqual("PASS ****", LoggedTunnel("PASS s3cret-pw"u8.ToArray()));

    [TestMethod]
    [DataRow((byte)0x16)]
    [DataRow((byte)0x00)]
    [DataRow((byte)0xFF)]
    public void Tunnel_OfAnotherBinaryProtocol_IsLoggedByteForByteAndNeverHeld(byte first)
    {
        byte[] opening = [first, 3, 1, 0, 5];
        var later = SocksTestKit.Latin1Bytes(SecretLines + "no line end");
        var output = new ArrayBufferWriter<byte>();
        var redactor = GrantedSocks5Tunnel(withLogin: false, output);

        redactor.Redact(opening, output);
        redactor.ObserveSent("220 ready\r\n"u8);
        redactor.Redact(later, output);
        CollectionAssert.AreEqual((byte[])[.. opening, .. later], output.WrittenSpan.ToArray());

        redactor.Flush(output);
        CollectionAssert.AreEqual((byte[])[.. opening, .. later], output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void Tunnel_AnOriginSendingWhatLooksLikeASuccessReply_DoesNotRestartTheTunnel()
    {
        var output = new ArrayBufferWriter<byte>();
        var redactor = GrantedSocks5Tunnel(withLogin: false, output);

        redactor.Redact("PASS s3c"u8, output);
        redactor.ObserveSent(SocksTestKit.Granted);
        redactor.Redact("ret-pw\r\n"u8, output);

        Assert.AreEqual("PASS ****\r\n", Encoding.Latin1.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void RefusedRequest_LeavesTheNextBytesUnchanged()
    {
        var logged = Logged([(SocksTestKit.Rejected, []), (SocksTestKit.Socks5Failure(5), [SocksTestKit.Latin1Bytes(SecretLines)])]);

        Assert.AreEqual(SecretLines, Encoding.Latin1.GetString(logged));
    }

    [TestMethod]
    public async Task BugReport20261125_FtpAndHttpThroughSocks5h_LogNoPassword()
    {
        var ftp = await LoggedSocks5ExchangeAsync(18021, "220 ready\r\n", "USER app\r\n", "PASS s3cret-pw\r\n", "QUIT\r\n");
        var http = await LoggedSocks5ExchangeAsync(
            18080,
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n",
            "GET /hello.txt HTTP/1.1\r\nHost: localhost:18080\r\nAuthorization: Basic YXBwOnMzY3JldC1wdw==\r\n\r\n");

        StringAssert.Contains(ftp, "USER app\r\nPASS ****\r\nQUIT\r\n");
        StringAssert.Contains(http, "GET /hello.txt HTTP/1.1\r\nHost: localhost:18080\r\nAuthorization: Basic ****\r\n\r\n");
        foreach (var logged in new[] { ftp, http })
        {
            Assert.IsFalse(logged.Contains("s3cret-pw", StringComparison.Ordinal), logged);
            Assert.IsFalse(logged.Contains("YXBwOnMzY3JldC1wdw==", StringComparison.Ordinal), logged);
        }
    }

    // Serves the report's SOCKS5 login and CONNECT, then the tunnelled lines, and returns the text the
    // levelled log was given: the connection's bytes through the server's redactor, as the engine does it.
    private static async Task<string> LoggedSocks5ExchangeAsync(int port, string originSends, params string[] tunnelled)
    {
        var log = new RecordingExchangeLog();
        var server = new SocksProtocolServer(
            new InMemoryOriginConnector().ScriptConnection(
                "localhost",
                port,
                SocksTestKit.ConnectionOf(SocksTestKit.Latin1Bytes(originSends))),
            new ScriptedProxyAccounts(refused: PasswordLoginVerdict.Accepted));
        byte[][] chunks =
        [
            Greeting,
            [1, 3, .. "app"u8, 9, .. "s3cret-pw"u8],
            SocksTestKit.Socks5DomainRequest("localhost", port),
            .. tunnelled.Select(SocksTestKit.Latin1Bytes),
        ];
        var redactor = server.CreateRedactor(new ExchangeRedactionContext(1, "socks5h"));
        var client = new RedactingLogConnection(SocksTestKit.ConnectionOf(chunks), redactor, log);

        await server.ServeAsync(client, SocksTestKit.Context(log, scheme: "socks5h"));
        client.FlushLog();

        return Encoding.Latin1.GetString([.. log.Entries
            .Where(entry => entry.Kind == ExchangeLogEntryKind.BytesReceived)
            .SelectMany(entry => entry.Bytes.ToArray())]);
    }
}
