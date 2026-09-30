using Surl.Protocol.Abstractions;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

[TestClass]
public sealed class SshProtocolServerTests
{
    private const string NegotiatedWithCurl =
        "SSH negotiated kex diffie-hellman-group-exchange-sha256, host key rsa-sha2-512, "
        + "cipher chacha20-poly1305@openssh.com/chacha20-poly1305@openssh.com, MAC implicit/implicit, "
        + "compression {0}/{0}, strict kex on";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("sftp-insecure", "none", DisplayName = "sftp -k")]
    [DataRow("sftp-insecure-compressed", "zlib", DisplayName = "sftp -k --compressed-ssh")]
    public async Task RecordedCurlOpening_IsAnsweredWithTheIdentificationLineAndKexInit_AndNegotiated(string caseName, string compression)
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([RecordedFixture.ReadRequestBytes(caseName)]);

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(Concat(Ascii(ServerLine), ExpectedServerKexInitPacket()), connection.WrittenBytes);
        CollectionAssert.AreEqual(
            new[] { "SSH client identification: SSH-2.0-libssh2_1.11.1", string.Format(null, NegotiatedWithCurl, compression) },
            log.Notes.ToArray());
    }

    [TestMethod]
    [DataRow("sftp-insecure")]
    [DataRow("sftp-insecure-compressed")]
    public void RecordedCurlOpening_EndedWhenTheRecorderHungUp(string caseName)
    {
        Assert.AreEqual("2", Text(RecordedFixture.ReadBytes(caseName, "exitcode.txt")));
        Assert.IsEmpty(RecordedFixture.ReadBytes(caseName, "stdout.bin"));
    }

    [TestMethod]
    public async Task UnbuiltKeyExchangeMethodMessage_IsAnsweredDisconnect11()
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(Ascii(ClientLine), Packet(ClientKexInitPayload(keyExchange: "diffie-hellman-group14-sha1")), Packet(30, 0, 0, 0, 0));

        await Server(OfferWithAnUnbuiltKeyExchange).ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ServerKexInitPacket(OfferWithAnUnbuiltKeyExchange), ServerDisconnectPacket(11, "Key exchange not implemented")),
            connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual(
            "The SSH key exchange diffie-hellman-group14-sha1 is not built yet; the connection was ended after the negotiation.",
            log.Notes[2]);
        Assert.AreEqual("SSH disconnect sent: 11 Key exchange not implemented", log.Notes[3]);
    }

    [TestMethod]
    public async Task RecordedCurlOpeningFollowedByTheOldGroupExchangeRequest_IsAnsweredDisconnect2()
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(RecordedFixture.ReadRequestBytes("sftp-insecure"), Packet(30, 0, 0, 8, 0));

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ExpectedServerKexInitPacket(), ServerDisconnectPacket(2, "Protocol error")),
            connection.WrittenBytes);
        Assert.AreEqual("The client sent SSH message 30 during the key exchange.", log.Notes[2]);
    }

    [TestMethod]
    public async Task PacketOverMaxMessageBytes_IsAnsweredDisconnect2BeforeItsBodyIsRead()
    {
        var log = new RecordingExchangeLog();
        var limits = ExchangeLimits.Default with { MaxMessageBytes = 1000 };
        var connection = new InMemoryConnection(
            [RecordedFixture.ReadRequestBytes("sftp-insecure")[..(ClientLine.Length + 4)]],
            peerHalfClosesWhenExhausted: false);

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, limits, log));

        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ServerKexInitPacket(), ServerDisconnectPacket(2, "Protocol error")),
            connection.WrittenBytes);
        Assert.AreEqual("An SSH packet announced 1088 bytes, over the 1000-byte packet limit.", log.Notes[1]);
    }

    [TestMethod]
    public async Task NoAlgorithmInCommon_IsAnsweredDisconnect3()
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(Ascii(ClientLine), Packet(ClientKexInitPayload(cipher: "3des-cbc,arcfour")));

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ServerKexInitPacket(), ServerDisconnectPacket(3, "No common algorithm")),
            connection.WrittenBytes);
        Assert.AreEqual("SSH no common cipher algorithm; client offered 3des-cbc,arcfour", log.Notes[1]);
        Assert.AreEqual("SSH disconnect sent: 3 No common algorithm", log.Notes[2]);
    }

    [TestMethod]
    [DataRow("SSH-1.5-old\r\n", "not an SSH 2.0 one: SSH-1.5-old", DisplayName = "An SSH 1 line")]
    [DataRow("HELLO\r\n", "not an SSH 2.0 one: HELLO", DisplayName = "Not an identification line")]
    [DataRow("\r\n", "not an SSH 2.0 one: ", DisplayName = "An empty line")]
    [DataRow("\n", "not an SSH 2.0 one: ", DisplayName = "A bare line feed")]
    [DataRow("SSH-2.0-a\0b\r\n", @"not an SSH 2.0 one: SSH-2.0-a\x00b", DisplayName = "A NUL in the line")]
    public async Task RefusedIdentificationLine_IsAnsweredDisconnect8(string line, string noteEnd)
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(Ascii(line));

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ServerDisconnectPacket(8, "Protocol version not supported")),
            connection.WrittenBytes);
        Assert.IsTrue(connection.WritesCompleted);
        Assert.AreEqual("The client's SSH identification line is " + noteEnd, log.Notes[0]);
    }

    [TestMethod]
    [DataRow(254, DisplayName = "256 bytes with CR LF")]
    [DataRow(300, DisplayName = "300 bytes and no line ending")]
    public async Task IdentificationLineOver255Bytes_IsAnsweredDisconnect8(int lineBytes)
    {
        var log = new RecordingExchangeLog();
        var line = "SSH-2.0-" + new string('a', lineBytes - 8) + (lineBytes == 254 ? "\r\n" : string.Empty);
        var connection = new InMemoryConnection([Ascii(line)], peerHalfClosesWhenExhausted: false);

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ServerDisconnectPacket(8, "Protocol version not supported")),
            connection.WrittenBytes);
        Assert.AreEqual("The client's SSH identification line was longer than 255 bytes.", log.Notes[0]);
    }

    [TestMethod]
    [DataRow("SSH-2.0-" + "x", "\r\n", DisplayName = "CR LF")]
    [DataRow("SSH-2.0-" + "x", "\n", DisplayName = "A bare LF")]
    [DataRow("SSH-1.99-compat", "\r\n", DisplayName = "SSH-1.99-")]
    public async Task AcceptedIdentificationLine_IsNotedWithoutItsEnding(string line, string ending)
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(Ascii(line + ending));

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(Concat(Ascii(ServerLine), ServerKexInitPacket()), connection.WrittenBytes);
        Assert.AreEqual("SSH client identification: " + line, log.Notes.Single());
    }

    [TestMethod]
    public async Task IdentificationLineOf255BytesWithCrLf_IsAccepted()
    {
        var log = new RecordingExchangeLog();
        var line = "SSH-2.0-" + new string('a', 245);
        var connection = Connection(Ascii(line + "\r\n"));

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        Assert.AreEqual("SSH client identification: " + line, log.Notes.Single());
    }

    [TestMethod]
    public async Task ClientClosesBeforeSendingAnything_EndsWithNoReplyAndNoNote()
    {
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([]);

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(Ascii(ServerLine), connection.WrittenBytes);
        Assert.IsEmpty(log.Notes);
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ClientClosesPartWayThroughItsIdentificationLine_IsNoted()
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(Ascii("SSH-2.0-li"));

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(Ascii(ServerLine), connection.WrittenBytes);
        Assert.AreEqual("The client closed the connection part way through its SSH identification line.", log.Notes.Single());
    }

    [TestMethod]
    public async Task ClientClosesPartWayThroughAPacket_IsNotedWithNoReply()
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(RecordedFixture.ReadRequestBytes("sftp-insecure")[..^10]);

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(Concat(Ascii(ServerLine), ServerKexInitPacket()), connection.WrittenBytes);
        Assert.AreEqual("The client closed the connection part way through an SSH packet.", log.Notes[1]);
        Assert.IsFalse(connection.WritesCompleted);
    }

    [TestMethod]
    public async Task ClientDisconnect_IsNotedAndNotAnswered()
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(Ascii(ClientLine), DisconnectPacket(11, "bye\n"));

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(Concat(Ascii(ServerLine), ServerKexInitPacket()), connection.WrittenBytes);
        Assert.AreEqual(@"SSH disconnect received: 11 bye\n", log.Notes[1]);
        Assert.AreEqual(2, log.Notes.Count);
    }

    [TestMethod]
    public async Task IgnoreDebugAndUnimplemented_AreSkippedAroundANonStrictKexInit()
    {
        var connection = Connection(
            Ascii(ClientLine),
            Packet(Concat([2], String("padding"))),
            Packet(Concat([4, 1], String("debug"), String(string.Empty))),
            Packet(ClientKexInitPayload(keyExchange: "diffie-hellman-group14-sha1")),
            Packet(Concat([3], UInt32(7))),
            Packet(Concat([2], String(string.Empty))),
            Packet(30, 0));

        await Server(OfferWithAnUnbuiltKeyExchange).ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken));

        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ServerKexInitPacket(OfferWithAnUnbuiltKeyExchange), ServerDisconnectPacket(11, "Key exchange not implemented")),
            connection.WrittenBytes);
    }

    [TestMethod]
    [DataRow(5, DisplayName = "SERVICE_REQUEST")]
    [DataRow(30, DisplayName = "A key exchange method message")]
    public async Task OtherMessageBeforeKexInit_IsAnsweredDisconnect2(int messageNumber)
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(Ascii(ClientLine), Packet((byte)messageNumber, 0, 0));

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ServerKexInitPacket(), ServerDisconnectPacket(2, "Protocol error")),
            connection.WrittenBytes);
        Assert.AreEqual($"The client sent SSH message {messageNumber} before its KEXINIT.", log.Notes[1]);
    }

    [TestMethod]
    public async Task OtherMessageDuringTheKeyExchange_IsAnsweredDisconnect2()
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(Ascii(ClientLine), Packet(ClientKexInitPayload()), Packet(21));

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ServerKexInitPacket(), ServerDisconnectPacket(2, "Protocol error")),
            connection.WrittenBytes);
        Assert.AreEqual("The client sent SSH message 21 during the key exchange.", log.Notes[2]);
    }

    [TestMethod]
    public async Task StrictKexInitAfterAnIgnore_IsAnsweredDisconnect2()
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(
            Ascii(ClientLine),
            Packet(Concat([2], String(string.Empty))),
            Packet(ClientKexInitPayload(keyExchange: "diffie-hellman-group14-sha256,kex-strict-c-v00@openssh.com")));

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ServerKexInitPacket(), ServerDisconnectPacket(2, "Protocol error")),
            connection.WrittenBytes);
        Assert.AreEqual("Under strict key exchange the client's KEXINIT was not its first SSH packet.", log.Notes[1]);
    }

    [TestMethod]
    public async Task IgnoreDuringAStrictKeyExchange_IsAnsweredDisconnect2()
    {
        var log = new RecordingExchangeLog();
        var connection = Connection(
            Ascii(ClientLine),
            Packet(ClientKexInitPayload(keyExchange: "diffie-hellman-group14-sha256,kex-strict-c-v00@openssh.com")),
            Packet(Concat([2], String(string.Empty))));

        await Server().ServeAsync(connection, Context(TimeProvider.System, TestContext.CancellationToken, log: log));

        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ServerKexInitPacket(), ServerDisconnectPacket(2, "Protocol error")),
            connection.WrittenBytes);
        Assert.AreEqual("The client sent SSH message 2 during the key exchange.", log.Notes[2]);
    }

    [TestMethod]
    public async Task WronglyGuessedKeyExchangePacket_IsDiscarded()
    {
        var guessOnly = Connection(
            Ascii(ClientLine),
            Packet(ClientKexInitPayload(keyExchange: "curve25519-sha256@libssh.org", firstKexPacketFollows: true)),
            Packet(30, 1, 2));
        var guessAndNext = Connection(
            Ascii(ClientLine),
            Packet(ClientKexInitPayload(keyExchange: "curve25519-sha256@libssh.org", firstKexPacketFollows: true)),
            Packet(30, 1, 2),
            Packet(30, 3, 4));

        await Server().ServeAsync(guessOnly, Context(TimeProvider.System, TestContext.CancellationToken));
        await Server().ServeAsync(guessAndNext, Context(TimeProvider.System, TestContext.CancellationToken));

        CollectionAssert.AreEqual(Concat(Ascii(ServerLine), ServerKexInitPacket()), guessOnly.WrittenBytes);
        CollectionAssert.AreEqual(
            Concat(Ascii(ServerLine), ServerKexInitPacket(), ServerDisconnectPacket(2, "Protocol error")),
            guessAndNext.WrittenBytes,
            "The second message 30 is read as the real ECDH_INIT, and its truncated key is refused.");
    }

    [TestMethod]
    public async Task OpeningNotDoneWithinTheHeadTimeout_ClosesWithNoReply()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new InMemoryConnection([Ascii(ClientLine)], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        clock.Advance(ExchangeLimits.Default.HeadTimeout - TimeSpan.FromTicks(1));
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await serving;

        CollectionAssert.AreEqual(Concat(Ascii(ServerLine), ServerKexInitPacket()), connection.WrittenBytes);
        Assert.IsFalse(connection.WritesCompleted);
        Assert.AreEqual("The SSH connection was not open within the head timeout; closed with no reply.", log.Notes[1]);
    }

    [TestMethod]
    public async Task ExchangeCancelledDuringTheOpening_PropagatesTheCancellation()
    {
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new InMemoryConnection([], peerHalfClosesWhenExhausted: false);

        var serving = Server().ServeAsync(connection, Context(new ManualTimeProvider(), exchange.Token));
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public async Task DisconnectNotTakenWithinTheWriteDeadline_ClosesWithoutAborting()
    {
        var clock = new ManualTimeProvider();
        var log = new RecordingExchangeLog();
        var connection = new WriteStallingConnection([Ascii("HELLO\r\n")], writesBeforeStalling: 1);

        var serving = Server().ServeAsync(connection, Context(clock, TestContext.CancellationToken, log: log));
        await connection.WriteStalled.WaitAsync(TestContext.CancellationToken);
        Assert.IsFalse(serving.IsCompleted);
        clock.Advance(SshProtocolServer.DisconnectWriteDeadline);
        await serving;

        CollectionAssert.AreEqual(Ascii(ServerLine), connection.WrittenBytes);
        Assert.IsFalse(connection.Aborted);
        Assert.AreEqual("The DISCONNECT was not written within the one-second write deadline; the connection was closed.", log.Notes[2]);
    }

    [TestMethod]
    public async Task ExchangeCancelledWhileTheDisconnectIsWritten_PropagatesTheCancellation()
    {
        using var exchange = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var connection = new WriteStallingConnection([Ascii("HELLO\r\n")], writesBeforeStalling: 1);

        var serving = Server().ServeAsync(connection, Context(new ManualTimeProvider(), exchange.Token));
        await connection.WriteStalled.WaitAsync(TestContext.CancellationToken);
        await exchange.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => serving);
    }

    [TestMethod]
    public void Schemes_AreScpAndSftp()
    {
        CollectionAssert.AreEqual(new[] { "scp", "sftp" }, Server().Schemes.ToArray());
    }

    [TestMethod]
    public async Task NullArguments_AreRefused()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SshProtocolServer(null!, RsaOffer, new AnonymousAuthenticationPolicy(), new FixedRandomSource()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SshProtocolServer(RsaHostKeys, null!, new AnonymousAuthenticationPolicy(), new FixedRandomSource()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SshProtocolServer(RsaHostKeys, RsaOffer, new AnonymousAuthenticationPolicy(), null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SshProtocolServer(RsaHostKeys, RsaOffer, null!, new FixedRandomSource()));
        var unsigned = Assert.ThrowsExactly<ArgumentException>(
            () => new SshProtocolServer(RsaHostKeys, SshAlgorithmOffer.Default(["ecdsa-sha2-nistp256", "rsa-sha2-256"], aesGcmIsSupported: false), new AnonymousAuthenticationPolicy(), new FixedRandomSource()));
        StringAssert.StartsWith(unsigned.Message, "The offer names the host-key algorithm ecdsa-sha2-nistp256, but no host key given signs with it.");
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => Server().ServeAsync(null!, Context(TimeProvider.System, TestContext.CancellationToken)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => Server().ServeAsync(new InMemoryConnection([]), null!));
    }

    // The server's KEXINIT as ADR-0051 decision 2 lists it for an RSA host key, written out
    // here rather than taken from the server's writer; cookie and padding are FixedRandomSource's.
    private static byte[] ExpectedServerKexInitPacket()
    {
        const string KeyExchange =
            "curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,ecdh-sha2-nistp384,"
            + "ecdh-sha2-nistp521,diffie-hellman-group-exchange-sha256,diffie-hellman-group16-sha512,"
            + "diffie-hellman-group18-sha512,diffie-hellman-group14-sha256,kex-strict-s-v00@openssh.com";
        const string Cipher =
            "chacha20-poly1305@openssh.com,aes256-gcm@openssh.com,aes128-gcm@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr";
        const string Mac = "hmac-sha2-256-etm@openssh.com,hmac-sha2-512-etm@openssh.com,hmac-sha2-256,hmac-sha2-512";
        const string Compression = "none,zlib@openssh.com,zlib";

        var payload = Concat(
            [20],
            Enumerable.Repeat(RandomByte, 16).ToArray(),
            String(KeyExchange),
            String("rsa-sha2-512,rsa-sha2-256"),
            String(Cipher),
            String(Cipher),
            String(Mac),
            String(Mac),
            String(Compression),
            String(Compression),
            String(string.Empty),
            String(string.Empty),
            [0],
            UInt32(0));
        var packet = Packet(payload);
        packet.AsSpan(packet.Length - packet[4]).Fill(RandomByte);

        return packet;
    }

    private static string Text(byte[] bytes) => System.Text.Encoding.ASCII.GetString(bytes);
}
