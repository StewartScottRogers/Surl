using System.Net;
using Surl.Protocol.Abstractions;
using static Surl.Protocol.Socks.SocksTestKit;

namespace Surl.Protocol.Socks;

/// <summary>
/// The SOCKS5 exchange against fixtures recorded from pinned upstream curl 8.21.0
/// (Fixtures/README.md) and against requests a peer may send but curl never does (ADR-0121
/// decisions 3, 4 and 6).
/// </summary>
[TestClass]
public sealed class Socks5ExchangeTests
{
    private static readonly byte[] Loopback = [127, 0, 0, 1];

    private static readonly byte[] NoAuthenticationOnly = [0x05, 0x01, 0x00];

    [TestMethod]
    [DataRow("socks5-connect", "socks5", 14, "127.0.0.1")]
    [DataRow("socks5h-host-name", "socks5h", 22, "origin.test")]
    [DataRow("socks5-ipv6", "socks5", 26, "::1")]
    public async Task ServeAsync_RecordedConnectWithoutAccounts_SelectsNoAuthenticationAndRelays(string fixture, string scheme, int handshakeBytes, string host)
    {
        var recorded = Fixture(fixture, "request.bin");
        var client = ConnectionOf(recorded[..handshakeBytes], recorded[handshakeBytes..]);
        var origin = OriginAt8080(Latin1Bytes(OriginResponse));
        var origins = new InMemoryOriginConnector().ScriptConnection(host, 8080, origin);

        await ServeAsync(client, origins, scheme);

        byte[] expected = [0x05, 0x00, .. Socks5Succeeded, .. Latin1Bytes(OriginResponse)];
        CollectionAssert.AreEqual(expected, client.WrittenBytes);
        CollectionAssert.AreEqual(recorded[handshakeBytes..], origin.WrittenBytes, "curl's GET reached the origin unchanged.");
        Assert.IsTrue(origin.WritesCompleted);
        Assert.IsTrue(client.WritesCompleted);
        Assert.AreEqual(TimeSpan.FromSeconds(30), origins.Requests.Single().Timeout);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedLoginToAnAccount_SelectsUserPasswordAcceptsAndRelays()
    {
        var recorded = Fixture("socks5h-user-password", "request.bin");
        var client = ConnectionOf(recorded);
        var origin = OriginAt8080(Latin1Bytes(OriginResponse));
        var accounts = new ScriptedProxyAccounts();
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector().ScriptConnection("origin.test", 8080, origin), "socks5h", accounts, log);

        byte[] expected = [0x05, 0x02, 0x01, 0x00, .. Socks5Succeeded, .. Latin1Bytes(OriginResponse)];
        CollectionAssert.AreEqual(expected, client.WrittenBytes);
        CollectionAssert.AreEqual(recorded[37..], origin.WrittenBytes);
        var login = accounts.Logins[^1];
        Assert.AreEqual("socks5h", login.Scheme);
        Assert.AreEqual("alice", login.UserName);
        Assert.IsNull(login.TlsSession);
        Assert.Contains("Login accepted: socks5h alice", log.Notes);
        Assert.IsFalse(log.Notes.Any(note => note.Contains("secret", StringComparison.Ordinal)), "The log never holds the password.");
    }

    [TestMethod]
    public async Task ServeAsync_RecordedWrongPassword_AnswersLoginRefusedAndCloses()
    {
        var client = HeldOpenConnectionOf(Fixture("socks5h-user-rejected", "request.bin"));
        var origins = new InMemoryOriginConnector();
        var log = new RecordingExchangeLog();

        await ServeAsync(client, origins, "socks5h", new ScriptedProxyAccounts(), log);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0x02, 0x01, 0x01 }, client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
        Assert.IsEmpty(origins.Requests);
        Assert.Contains("Login refused: socks5h alice", log.Notes);
        Assert.Contains("Answered 01 01: the login is RefusedCredentials.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_RecordedGreetingToAListenerWithAccounts_AnswersNoAcceptableMethod()
    {
        var client = HeldOpenConnectionOf(Fixture("socks5-no-acceptable-method", "request.bin"));
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector(), "socks5", new ScriptedProxyAccounts(), log);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0xFF }, client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
        Assert.Contains("No SOCKS5 method offered is acceptable; answered 05 FF.", log.Notes);
    }

    [TestMethod]
    [DataRow(OriginConnectionFailure.NotAllowed, (byte)0x02)]
    [DataRow(OriginConnectionFailure.Unreachable, (byte)0x03)]
    [DataRow(OriginConnectionFailure.HostNotFound, (byte)0x04)]
    [DataRow(OriginConnectionFailure.Refused, (byte)0x05)]
    [DataRow(OriginConnectionFailure.TimedOut, (byte)0x06)]
    [DataRow((OriginConnectionFailure)99, (byte)0x01)]
    public async Task ServeAsync_OriginNotConnected_AnswersTheFailuresReplyCodeAndCloses(OriginConnectionFailure failure, byte code)
    {
        var client = ConnectionOf(Fixture("socks5-connection-refused", "request.bin"));
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector().ScriptFailure("127.0.0.1", 8080, failure), "socks5", log: log);

        CollectionAssert.AreEqual((byte[])[0x05, 0x00, .. Socks5Failure(code)], client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
        Assert.Contains($"Answered 05 {code:X2}: the origin 127.0.0.1:8080 could not be used ({failure}).", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_RefusedIPv6Origin_NamesItInBrackets()
    {
        var client = ConnectionOf(Fixture("socks5-ipv6", "request.bin")[..26]);
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector().ScriptFailure("::1", 8080, OriginConnectionFailure.Refused), "socks5", log: log);

        Assert.Contains("Answered 05 05: the origin [::1]:8080 could not be used (Refused).", log.Notes);
    }

    [TestMethod]
    [DataRow((byte)0x02)]
    [DataRow((byte)0x03)]
    [DataRow((byte)0x09)]
    public async Task ServeAsync_BindUdpAssociateOrAnyOtherCommand_AnswersCommandNotSupported(byte command)
    {
        var client = ConnectionOf(NoAuthenticationOnly, Socks5Request(command, 0x01, Loopback, 8080));
        var origins = new InMemoryOriginConnector();
        var log = new RecordingExchangeLog();

        await ServeAsync(client, origins, "socks5", log: log);

        CollectionAssert.AreEqual((byte[])[0x05, 0x00, .. Socks5Failure(0x07)], client.WrittenBytes);
        Assert.IsEmpty(origins.Requests);
        Assert.Contains($"Answered 05 07: the command 0x{command:X2} is not CONNECT.", log.Notes);
    }

    [TestMethod]
    [DataRow((byte)0x00)]
    [DataRow((byte)0x02)]
    [DataRow((byte)0x05)]
    public async Task ServeAsync_UnknownAddressType_AnswersAddressTypeNotSupportedWithoutReadingFurther(byte addressType)
    {
        var client = HeldOpenConnectionOf(NoAuthenticationOnly, [0x05, 0x01, 0x00, addressType]);
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector(), "socks5", log: log);

        CollectionAssert.AreEqual((byte[])[0x05, 0x00, .. Socks5Failure(0x08)], client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
        Assert.Contains("Answered 05 08: the address type is not 01, 03 or 04.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_EmptyDomainName_AnswersHostUnreachable()
    {
        var client = ConnectionOf(NoAuthenticationOnly, Socks5DomainRequest(string.Empty, 8080));
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector(), "socks5h", log: log);

        CollectionAssert.AreEqual((byte[])[0x05, 0x00, .. Socks5Failure(0x04)], client.WrittenBytes);
        Assert.Contains("Answered 05 04: the domain name is empty (HostNotFound).", log.Notes);
    }

    [TestMethod]
    [DataRow((byte)0x01, new byte[] { 0, 0, 0, 0 }, 8080, "the address 0.0.0.0 is not allowed (NotAllowed)")]
    [DataRow((byte)0x04, new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 8080, "the address :: is not allowed (NotAllowed)")]
    [DataRow((byte)0x01, new byte[] { 127, 0, 0, 1 }, 0, "port 0 cannot be connected (NotAllowed)")]
    public async Task ServeAsync_UnspecifiedAddressOrPortZero_AnswersNotAllowed(byte addressType, byte[] address, int port, string why)
    {
        var client = ConnectionOf(NoAuthenticationOnly, Socks5Request(0x01, addressType, address, port));
        var origins = new InMemoryOriginConnector();
        var log = new RecordingExchangeLog();

        await ServeAsync(client, origins, "socks5", log: log);

        CollectionAssert.AreEqual((byte[])[0x05, 0x00, .. Socks5Failure(0x02)], client.WrittenBytes);
        Assert.IsEmpty(origins.Requests);
        Assert.Contains($"Answered 05 02: {why}.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_DomainNameOf255Bytes_IsConnected()
    {
        var host = new string('h', 255);
        var origin = OriginAt8080();
        var client = ConnectionOf(NoAuthenticationOnly, Socks5DomainRequest(host, 8080));

        await ServeAsync(client, new InMemoryOriginConnector().ScriptConnection(host, 8080, origin), "socks5h");

        CollectionAssert.AreEqual((byte[])[0x05, 0x00, .. Socks5Succeeded], client.WrittenBytes);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(15)]
    [DataRow(21)]
    public async Task ServeAsync_HandshakeEndsEarly_ClosesWithoutAnotherReply(int bytesSent)
    {
        byte[] handshake = [0x05, 0x02, 0x00, 0x01, .. Socks5DomainRequest("origin.test", 8080)];
        var client = ConnectionOf(handshake[..bytesSent]);
        var origins = new InMemoryOriginConnector();

        await ServeAsync(client, origins, "socks5h");

        var expected = bytesSent < 4 ? Array.Empty<byte>() : new byte[] { 0x05, 0x00 };
        CollectionAssert.AreEqual(expected, client.WrittenBytes);
        Assert.IsEmpty(origins.Requests);
    }

    [TestMethod]
    public async Task ServeAsync_TruncatedGreeting_NotesItAndWritesNothing()
    {
        var client = ConnectionOf([0x05, 0x02, 0x00]);
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector(), "socks5", log: log);

        Assert.IsEmpty(client.WrittenBytes);
        Assert.Contains("curl closed the connection before the SOCKS5 greeting ended; no reply.", log.Notes);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x05, 0x01, 0x00, 0x01, 127, 0 }, "curl closed the connection before the SOCKS5 request ended; no reply.")]
    [DataRow(new byte[] { 0x05, 0x01, 0x00, 0x01, 127, 0, 0, 1, 0x1F }, "curl closed the connection before the SOCKS5 request ended; no reply.")]
    [DataRow(new byte[] { 0x05, 0x01, 0x00, 0x03, 0x05, 0x61 }, "curl closed the connection before the SOCKS5 request ended; no reply.")]
    [DataRow(new byte[] { 0x04, 0x01, 0x00, 0x01, 127, 0, 0, 1, 0x1F, 0x90 }, "The SOCKS5 request's version is not 05; the connection closes without a reply.")]
    public async Task ServeAsync_RequestTruncatedOrOfAnotherVersion_ClosesWithoutAReply(byte[] request, string note)
    {
        var client = ConnectionOf(NoAuthenticationOnly, request);
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector(), "socks5", log: log);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0x00 }, client.WrittenBytes);
        Assert.Contains(note, log.Notes);
    }

    [TestMethod]
    [DataRow(new byte[0], "curl closed the connection before the SOCKS5 login; no reply.")]
    [DataRow(new byte[] { 0x05, 0x05 }, "The SOCKS5 login's version 0x05 is not 01; the connection closes without a reply.")]
    [DataRow(new byte[] { 0x01, 0x05, 0x61 }, "curl closed the connection before the SOCKS5 login ended; no reply.")]
    [DataRow(new byte[] { 0x01, 0x01, 0x61, 0x02, 0x62 }, "curl closed the connection before the SOCKS5 login ended; no reply.")]
    public async Task ServeAsync_LoginTruncatedOrOfAnotherVersion_ClosesWithoutALoginReply(byte[] login, string note)
    {
        var client = ConnectionOf([0x05, 0x01, 0x02], login);
        var accounts = new ScriptedProxyAccounts();
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector(), "socks5", accounts, log);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0x02 }, client.WrittenBytes);
        Assert.IsEmpty(accounts.Logins);
        Assert.Contains(note, log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ClearPasswordWithoutAllowPlaintextAuth_AnswersLoginRefusedAndSaysWhy()
    {
        var client = HeldOpenConnectionOf(Fixture("socks5h-user-password", "request.bin")[..19]);
        var log = new RecordingExchangeLog();
        var accounts = new ScriptedProxyAccounts(refused: PasswordLoginVerdict.RefusedPlaintext, alice: PasswordLoginVerdict.RefusedPlaintext);

        await ServeAsync(client, new InMemoryOriginConnector(), "socks5h", accounts, log);

        CollectionAssert.AreEqual(new byte[] { 0x05, 0x02, 0x01, 0x01 }, client.WrittenBytes);
        Assert.IsTrue(client.WritesCompleted);
        Assert.Contains(Socks5Exchange.PlaintextRefusedNote, log.Notes);
    }

    [TestMethod]
    [DataRow(PasswordLoginVerdict.RefusedAnonymous, new byte[] { 0x01, 0x01 })]
    [DataRow(PasswordLoginVerdict.AcceptedUnchecked, new byte[] { 0x01, 0x00 })]
    public async Task ServeAsync_LoginJudgedWithoutACheck_AnswersTheVerdictWithoutALoginNote(PasswordLoginVerdict verdict, byte[] reply)
    {
        var client = ConnectionOf(Fixture("socks5h-user-rejected", "request.bin"));
        var log = new RecordingExchangeLog();

        await ServeAsync(client, new InMemoryOriginConnector(), "socks5h", new ScriptedProxyAccounts(refused: verdict), log);

        CollectionAssert.AreEqual((byte[])[0x05, 0x02, .. reply], client.WrittenBytes[..4]);
        Assert.IsFalse(log.Notes.Any(note => note.StartsWith("Login ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ServeAsync_AccountsAndAllowAnonymous_SelectsNoAuthenticationWhenOffered()
    {
        var origin = OriginAt8080();
        var client = ConnectionOf(Fixture("socks5-connection-refused", "request.bin"));
        var accounts = new ScriptedProxyAccounts(anonymous: PasswordLoginVerdict.AcceptedUnchecked);

        await ServeAsync(client, new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, origin), "socks5", accounts);

        CollectionAssert.AreEqual((byte[])[0x05, 0x00, .. Socks5Succeeded], client.WrittenBytes);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x05, 0x01, 0x02 })]
    [DataRow(new byte[] { 0x05, 0x00 })]
    [DataRow(new byte[] { 0x05, 0x01, 0x01 })]
    public async Task ServeAsync_NoAccountsAndNoAuthenticationNotOffered_AnswersNoAcceptableMethod(byte[] greeting)
    {
        var client = ConnectionOf(greeting);

        await ServeAsync(client, new InMemoryOriginConnector(), "socks5");

        CollectionAssert.AreEqual(new byte[] { 0x05, 0xFF }, client.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_OriginBoundToIPv6_AnswersItsAddressAsAtyp4()
    {
        var origin = new InMemoryConnection([], localEndPoint: new IPEndPoint(IPAddress.IPv6Loopback, 443));
        var client = ConnectionOf(NoAuthenticationOnly, Socks5Request(0x01, 0x01, Loopback, 8080));

        await ServeAsync(client, new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, origin), "socks5");

        byte[] expected = [0x05, 0x00, 0x05, 0x00, 0x00, 0x04, .. IPAddress.IPv6Loopback.GetAddressBytes(), 0x01, 0xBB];
        CollectionAssert.AreEqual(expected, client.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_OriginBoundToNoIPEndPoint_AnswersZeroAddressAndPort()
    {
        var origin = new InMemoryConnection([], localEndPoint: new DnsEndPoint("origin.test", 8080));
        var client = ConnectionOf(NoAuthenticationOnly, Socks5Request(0x01, 0x01, Loopback, 8080));

        await ServeAsync(client, new InMemoryOriginConnector().ScriptConnection("127.0.0.1", 8080, origin), "socks5");

        CollectionAssert.AreEqual((byte[])[0x05, 0x00, .. Socks5Failure(0x00)], client.WrittenBytes);
    }

    [TestMethod]
    public async Task ServeAsync_HandshakeStallsPastTheHeadTimeout_ClosesWithoutAReply()
    {
        var clock = new ManualTimeProvider(Now);
        var client = HeldOpenConnectionOf(NoAuthenticationOnly, [0x05, 0x01]);
        var log = new RecordingExchangeLog();
        var serving = new SocksProtocolServer(new InMemoryOriginConnector()).ServeAsync(client, Context(log, clock, scheme: "socks5"));

        while (clock.ActiveTimerCount == 0)
        {
            await Task.Delay(1);
        }

        clock.Advance(TimeSpan.FromSeconds(30));
        await serving;

        CollectionAssert.AreEqual(new byte[] { 0x05, 0x00 }, client.WrittenBytes);
        Assert.Contains("The SOCKS5 handshake was not complete within the head timeout; the connection closes without a reply.", log.Notes);
    }

    [TestMethod]
    public async Task ServeAsync_ExchangeCancelledMidHandshake_LetsTheCancellationThrough()
    {
        using var cancellation = new CancellationTokenSource();
        var clock = new ManualTimeProvider(Now);
        var client = HeldOpenConnectionOf([0x05, 0x01]);
        var serving = new SocksProtocolServer(new InMemoryOriginConnector()).ServeAsync(client, Context(new RecordingExchangeLog(), clock, cancellation.Token, "socks5"));

        while (clock.ActiveTimerCount == 0)
        {
            await Task.Delay(1);
        }

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public void RefusalBeforeConnecting_ConnectToAnOrigin_IsNull()
    {
        var request = new Socks5Request(Socks5Request.ConnectCommand, IPAddress.IPv6Loopback, null, 8080);

        Assert.IsNull(Socks5Exchange.RefusalBeforeConnecting(request));
    }

    private static InMemoryConnection OriginAt8080(params byte[][] chunks) =>
        new(chunks.Select(chunk => (ReadOnlyMemory<byte>)chunk), localEndPoint: new IPEndPoint(IPAddress.Loopback, 8080));
}
