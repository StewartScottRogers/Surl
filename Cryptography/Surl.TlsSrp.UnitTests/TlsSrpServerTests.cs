using System.Security.Cryptography;
using Surl.Cryptography.Srp;

namespace Surl.TlsSrp;

/// <summary>
/// The server side of RFC 5054's handshake over RFC 5246 (ADR-0122 decisions 2, 3 and 6), driven by
/// the bytes pinned upstream curl 8.21.0 sent (Fixtures/README.md) and by a client written out from
/// the RFCs in <see cref="UnitTestSrpHandshake"/>.
/// </summary>
[TestClass]
public sealed class TlsSrpServerTests
{
    private const string Recorded = "CurlTlsSrpJsmithAbc";

    /// <summary>
    /// The plaintext request the recording relay forwarded (its request.bin), kept here rather than
    /// as a file so line-ending normalisation cannot change its CRLFs.
    /// </summary>
    private static readonly byte[] RecordedRequest =
        "GET / HTTP/1.1\r\nHost: 127.0.0.1:18566\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n"u8.ToArray();

    [TestMethod]
    public async Task AuthenticateAsync_RecordedUpstreamCurlJsmithAbc_SendsTheComputedFlightAndCarriesTheRequestAndReply()
    {
        var client = UnitTestFixtures.Read(Recorded, "client.bin");
        var records = UnitTestFixtures.Fragments(client);
        var hello = records[0];
        var keyExchange = records[1];
        var clientRandom = hello[6..38];
        byte[] serverFlight =
        [
            .. UnitTestSrpClient.ExpectedServerHello(
                0xC020,
                UnitTestSrpClient.Extension(TlsExtensionType.RenegotiationInfo, [0]),
                UnitTestSrpClient.Extension(TlsExtensionType.ExtendedMasterSecret, []),
                UnitTestSrpClient.Extension(TlsExtensionType.EncryptThenMac, []),
                UnitTestSrpClient.Extension(TlsExtensionType.ApplicationLayerProtocolNegotiation, UnitTestSrpClient.Vector16([8, .. "http/1.1"u8]))),
            .. UnitTestSrpClient.ExpectedServerKeyExchange(),
            .. UnitTestSrpClient.Handshake(TlsHandshakeType.ServerHelloDone, []),
        ];
        var group = UnitTestSrpClient.Group;
        var clientPublic = Srp6a.FromUnsignedBigEndian(keyExchange[6..]);
        var premaster = Srp6a.ToUnsignedBigEndian(Srp6a.ComputePremasterSecret(
            group,
            clientPublic,
            Srp6a.FromUnsignedBigEndian(UnitTestSrpClient.Verifier("jsmith", "abc")),
            Srp6a.ComputeScrambler(group, clientPublic, UnitTestSrpClient.ServerPublicValue()),
            Srp6a.FromUnsignedBigEndian(UnitTestSrpClient.ServerRandomPattern)));
        byte[] transcript = [.. hello, .. serverFlight, .. keyExchange];
        var masterSecret = TlsPseudorandomFunction.ExtendedMasterSecret(premaster, SHA256.HashData(transcript));
        var keys = TlsKeyBlock.Derive(TlsSrpCipherSuite.SrpShaAes256CbcSha, masterSecret, clientRandom, UnitTestSrpClient.ServerRandomPattern);
        var clientFinished = UnitTestSrpClient.Handshake(
            TlsHandshakeType.Finished, TlsPseudorandomFunction.ClientFinishedVerifyData(masterSecret, SHA256.HashData(transcript)));
        var serverFinished = UnitTestSrpClient.Handshake(
            TlsHandshakeType.Finished, TlsPseudorandomFunction.ServerFinishedVerifyData(masterSecret, SHA256.HashData([.. transcript, .. clientFinished])));
        var iv = UnitTestSrpClient.ServerRandomPattern[..16];
        byte[] expected =
        [
            .. UnitTestSrpClient.Record(TlsContentType.Handshake, serverFlight),
            .. UnitTestSrpHandshake.ChangeCipherSpecRecord,
            .. UnitTestSrpClient.Record(
                TlsContentType.Handshake,
                UnitTestCbcRecords.EncryptThenMac(TlsBulkCipher.Aes256Cbc, keys.ServerKey, keys.ServerMacKey, 0, TlsContentType.Handshake, iv, serverFinished, UnitTestCbcRecords.Padding(16))),
        ];
        using var transport = new UnitTestScriptedStream(client);

        var session = await UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None);
        var handshakeBytes = transport.Written.ToArray();
        var request = new byte[RecordedRequest.Length];
        await session.Stream.ReadExactlyAsync(request);
        var reply = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nFINE\n"u8.ToArray();
        await session.Stream.WriteAsync(reply);
        var end = await session.Stream.ReadAsync(new byte[1]);

        CollectionAssert.AreEqual(expected, handshakeBytes);
        CollectionAssert.AreEqual(RecordedRequest, request);
        var protectedRecords = new TlsRecordReader(new MemoryStream(transport.Written.ToArray()[(5 + serverFlight.Length + 6)..]));
        protectedRecords.ChangeProtection(keys.ServerWriteProtection(encryptThenMac: true, RandomNumberGenerator.Create()));
        CollectionAssert.AreEqual(serverFinished, (await protectedRecords.ReadAsync(CancellationToken.None))!.Fragment);
        var replyRecord = await protectedRecords.ReadAsync(CancellationToken.None);
        CollectionAssert.AreEqual(reply, replyRecord!.Fragment);
        Assert.AreEqual(0, end);
        Assert.AreEqual(new TlsSrpSession(session.Stream, TlsSrpCipherSuite.SrpShaAes256CbcSha, "jsmith", "http/1.1", null, true, true), session);
    }

    [TestMethod]
    public async Task AuthenticateAsync_RecordedUpstreamCurlHelloAndAnRsaCertificate_SendsRsaAes256CertificateAndAPssSignedKeyExchange()
    {
        var (messages, signedContent) = await ReplayRecordedHelloAsync(UnitTestCertificates.RsaCertificate());

        CollectionAssert.AreEqual(new byte[] { 0xC0, 0x21 }, messages[0].Body[35..37]);
        var signature = SignatureOf(messages[2].Body, 0x0804);
        Assert.IsTrue(UnitTestCertificates.Rsa.VerifyData(signedContent, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsync_RecordedUpstreamCurlHelloAndADsaCertificate_SendsDssAes256CertificateAndADsaSha256SignedKeyExchange()
    {
        var (messages, signedContent) = await ReplayRecordedHelloAsync(UnitTestCertificates.DsaCertificate());

        CollectionAssert.AreEqual(new byte[] { 0xC0, 0x22 }, messages[0].Body[35..37]);
        var signature = SignatureOf(messages[2].Body, 0x0402);
        Assert.IsTrue(UnitTestCertificates.Dsa.VerifySignature(SHA256.HashData(signedContent), signature, DSASignatureFormat.Rfc3279DerSequence));
    }

    [TestMethod]
    [DataRow("CurlTlsSrpJsmithBadpass")]
    [DataRow("CurlTlsSrpBaduserBadpass")]
    public async Task AuthenticateAsync_RecordedUpstreamCurlWithAWrongPasswordOrUnknownUser_SendsBadRecordMac(string caseName)
    {
        using var transport = new UnitTestScriptedStream(UnitTestFixtures.Read(caseName, "client.bin"));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert.Description);
        CollectionAssert.AreEqual(Convert.FromHexString("15030300020214"), transport.Written.ToArray()[^7..]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_RecordedUpstreamCurlUnknownUser_OffersTheSimulatedSaltAndAVerifierFromTheRandomSource()
    {
        using var transport = new UnitTestScriptedStream(UnitTestFixtures.Read("CurlTlsSrpBaduserBadpass", "client.bin"));
        var salt = HMACSHA256.HashData(new byte[32], "baduser"u8)[..16];
        var verifier = Enumerable.Range(0, 256).Select(index => UnitTestSrpClient.ServerRandomPattern[index % 32]).ToArray();
        var serverPublic = Srp6a.ToUnsignedBigEndian(Srp6a.ComputeServerPublicValue(
            UnitTestSrpClient.Group, Srp6a.FromUnsignedBigEndian(verifier), Srp6a.FromUnsignedBigEndian(UnitTestSrpClient.ServerRandomPattern)));
        byte[] keyExchangeTail = [.. UnitTestSrpClient.Vector8(salt), .. UnitTestSrpClient.Vector16(serverPublic)];

        await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        var written = transport.Written.ToArray();
        Assert.IsTrue(written.AsSpan().IndexOf(keyExchangeTail) > 0);
    }

    [TestMethod]
    [DataRow(false, false, false, (ushort)0xC020)]
    [DataRow(true, false, false, (ushort)0xC01D)]
    [DataRow(false, true, true, (ushort)0xC01A)]
    [DataRow(true, true, false, (ushort)0xC020)]
    public async Task AuthenticateAsync_ClientFromTheRfcs_CompletesAndCarriesApplicationDataBothWays(
        bool extendedMasterSecret, bool encryptThenMac, bool alpn, ushort suite)
    {
        var handshake = new UnitTestSrpHandshake(extendedMasterSecret: extendedMasterSecret, encryptThenMac: encryptThenMac, alpn: alpn, suite: suite);
        using var transport = new UnitTestScriptedStream(handshake.ClientBytes(null, "GET / HTTP/1.1\r\n\r\n"u8.ToArray()), chunkLength: 7);

        var session = await UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None);
        var request = new byte[18];
        await session.Stream.ReadExactlyAsync(request);
        await session.Stream.WriteAsync("HTTP/1.1 200 OK\r\n\r\n"u8.ToArray());

        var written = transport.Written.ToArray();
        CollectionAssert.AreEqual(UnitTestSrpClient.Record(TlsContentType.Handshake, handshake.ServerFlight), written[..(5 + handshake.ServerFlight.Length)]);
        var serverRecords = handshake.ServerRecordsAfterChangeCipherSpec(written);
        CollectionAssert.AreEqual(handshake.ServerFinished, (await serverRecords.ReadAsync(CancellationToken.None))!.Fragment);
        CollectionAssert.AreEqual("HTTP/1.1 200 OK\r\n\r\n"u8.ToArray(), (await serverRecords.ReadAsync(CancellationToken.None))!.Fragment);
        CollectionAssert.AreEqual("GET / HTTP/1.1\r\n\r\n"u8.ToArray(), request);
        Assert.AreEqual((TlsSrpCipherSuite)suite, session.CipherSuite);
        Assert.AreEqual(alpn ? "http/1.1" : null, session.ApplicationProtocol);
        Assert.AreEqual(extendedMasterSecret, session.ExtendedMasterSecret);
        Assert.AreEqual(encryptThenMac, session.EncryptThenMac);
    }

    [TestMethod]
    public async Task AuthenticateAsync_UnknownUser_SendsBadRecordMacAtTheClientsFinished()
    {
        var handshake = new UnitTestSrpHandshake(userName: "nobody");
        using var transport = new UnitTestScriptedStream(handshake.ClientBytes());

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert.Description);
        StringAssert.StartsWith(alert.Message, "The TLS-SRP login failed: unknown user 'nobody'. ");
        CollectionAssert.AreEqual(Convert.FromHexString("15030300020214"), transport.Written.ToArray()[^7..]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_WrongPassword_SendsBadRecordMacAtTheClientsFinished()
    {
        var handshake = new UnitTestSrpHandshake(password: "badpass");
        using var transport = new UnitTestScriptedStream(handshake.ClientBytes());

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.BadRecordMac, alert.Description);
        StringAssert.StartsWith(alert.Message, "The TLS-SRP login failed: wrong password for 'jsmith'. ");
        CollectionAssert.AreEqual(Convert.FromHexString("15030300020214"), transport.Written.ToArray()[^7..]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_FinishedWithWrongVerifyData_SendsDecryptError()
    {
        var handshake = new UnitTestSrpHandshake();
        byte[] finished = [.. handshake.ClientFinished[..^1], (byte)(handshake.ClientFinished[^1] ^ 1)];
        using var transport = new UnitTestScriptedStream(handshake.ClientBytes(finished));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.DecryptError, alert.Description);
        CollectionAssert.AreEqual(Convert.FromHexString("15030300020233"), transport.Written.ToArray()[^7..]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_ProtectedClientKeyExchangeInsteadOfFinished_SendsUnexpectedMessageWithoutALoginNote()
    {
        var handshake = new UnitTestSrpHandshake();
        using var transport = new UnitTestScriptedStream(handshake.ClientBytes(handshake.KeyExchange));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, alert.Description);
        Assert.DoesNotContain("TLS-SRP login", alert.Message);
    }

    [TestMethod]
    public async Task AuthenticateAsync_FinishedOfTheWrongLength_SendsDecodeError()
    {
        var handshake = new UnitTestSrpHandshake();
        using var transport = new UnitTestScriptedStream(handshake.ClientBytes(UnitTestSrpClient.Handshake(TlsHandshakeType.Finished, new byte[11])));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert.Description);
    }

    [TestMethod]
    public async Task AuthenticateAsync_ClientHelloWithoutTheSrpExtension_SendsUnknownPskIdentity()
    {
        var hello = UnitTestSrpClient.Handshake(TlsHandshakeType.ClientHello, UnitTestSrpClient.ClientHelloBody(0x0303, [0xC020], [0]));
        using var transport = new UnitTestScriptedStream(UnitTestSrpClient.Record(TlsContentType.Handshake, hello));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.UnknownPskIdentity, alert.Description);
        CollectionAssert.AreEqual(Convert.FromHexString("15030300020273"), transport.Written.ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsync_NoSharedCipherSuite_SendsHandshakeFailure()
    {
        var hello = UnitTestSrpClient.Handshake(
            TlsHandshakeType.ClientHello, UnitTestSrpClient.ClientHelloBody(0x0303, [0x1302, 0xC02F, 0xC021, 0xC022], [0], UnitTestSrpClient.SrpExtension("jsmith")));
        using var transport = new UnitTestScriptedStream(UnitTestSrpClient.Record(TlsContentType.Handshake, hello));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.HandshakeFailure, alert.Description);
        CollectionAssert.AreEqual(Convert.FromHexString("15030300020228"), transport.Written.ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsync_ClientKeyExchangeFirst_SendsUnexpectedMessage()
    {
        using var transport = new UnitTestScriptedStream(UnitTestSrpClient.Record(TlsContentType.Handshake, UnitTestSrpClient.ClientKeyExchange()));

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, alert.Description);
        CollectionAssert.AreEqual(Convert.FromHexString("1503030002020A"), transport.Written.ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsync_FinishedBeforeChangeCipherSpec_SendsUnexpectedMessage()
    {
        var handshake = new UnitTestSrpHandshake();
        byte[] client =
        [
            .. UnitTestSrpClient.Record(TlsContentType.Handshake, handshake.Hello),
            .. UnitTestSrpClient.Record(TlsContentType.Handshake, [.. handshake.KeyExchange, .. handshake.ClientFinished]),
        ];
        using var transport = new UnitTestScriptedStream(client);

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, alert.Description);
    }

    [TestMethod]
    public async Task AuthenticateAsync_ApplicationDataInsteadOfClientKeyExchange_SendsUnexpectedMessage()
    {
        byte[] client = [.. UnitTestSrpClient.Record(TlsContentType.Handshake, UnitTestSrpClient.ClientHello()), .. UnitTestSrpClient.Record(TlsContentType.ApplicationData, [1])];
        using var transport = new UnitTestScriptedStream(client);

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, alert.Description);
    }

    [TestMethod]
    public async Task AuthenticateAsync_ClientPublicValueAMultipleOfN_SendsIllegalParameter()
    {
        var keyExchange = UnitTestSrpClient.Handshake(
            TlsHandshakeType.ClientKeyExchange, UnitTestSrpClient.Vector16(Srp6a.ToUnsignedBigEndian(UnitTestSrpClient.Group.Prime)));
        byte[] client =
        [
            .. UnitTestSrpClient.Record(TlsContentType.Handshake, UnitTestSrpClient.ClientHello()),
            .. UnitTestSrpClient.Record(TlsContentType.Handshake, keyExchange),
        ];
        using var transport = new UnitTestScriptedStream(client);

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.IllegalParameter, alert.Description);
        CollectionAssert.AreEqual(Convert.FromHexString("1503030002022F"), transport.Written.ToArray()[^7..]);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(257)]
    public async Task AuthenticateAsync_ClientPublicValueEmptyOrLongerThanN_SendsDecodeError(int length)
    {
        var keyExchange = UnitTestSrpClient.Handshake(TlsHandshakeType.ClientKeyExchange, UnitTestSrpClient.Vector16(Enumerable.Repeat((byte)1, length).ToArray()));
        byte[] client =
        [
            .. UnitTestSrpClient.Record(TlsContentType.Handshake, UnitTestSrpClient.ClientHello()),
            .. UnitTestSrpClient.Record(TlsContentType.Handshake, keyExchange),
        ];
        using var transport = new UnitTestScriptedStream(client);

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.DecodeError, alert.Description);
    }

    [TestMethod]
    public async Task AuthenticateAsync_ClientSendsAnAlert_ThrowsWithoutReplying()
    {
        using var transport = new UnitTestScriptedStream(UnitTestSrpClient.Record(TlsContentType.Alert, [2, 40]));

        var alert = await Assert.ThrowsExactlyAsync<TlsPeerAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.IsTrue(alert.IsFatal);
        Assert.AreEqual((byte)40, alert.Description);
        Assert.AreEqual(0, transport.Written.Length);
    }

    [TestMethod]
    public async Task AuthenticateAsync_ClientHangsUpDuringTheHandshake_ThrowsEndOfStream()
    {
        using var transport = new UnitTestScriptedStream(UnitTestSrpClient.Record(TlsContentType.Handshake, UnitTestSrpClient.ClientHello()));

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));
    }

    [TestMethod]
    public async Task AuthenticateAsync_AlertCannotBeSent_StillThrowsTheAlert()
    {
        using var transport = new UnitTestScriptedStream(UnitTestSrpClient.Record(TlsContentType.Handshake, UnitTestSrpClient.ClientKeyExchange())) { FailWrites = true };

        var alert = await Assert.ThrowsExactlyAsync<TlsAlertException>(() => UnitTestSrpClient.Server().AuthenticateAsync(transport, CancellationToken.None));

        Assert.AreEqual(TlsAlertDescription.UnexpectedMessage, alert.Description);
    }

    /// <summary>
    /// Replays upstream curl's recorded client bytes against a server holding <paramref name="certificate"/>.
    /// The handshake ends at the client's Finished (the recorded keys were computed over the unsigned flight),
    /// but the first flight is complete by then.
    /// </summary>
    private static async Task<(List<(TlsHandshakeType Type, byte[] Body)> Messages, byte[] SignedContent)> ReplayRecordedHelloAsync(TlsSrpServerCertificate certificate)
    {
        var client = UnitTestFixtures.Read(Recorded, "client.bin");
        var clientRandom = UnitTestFixtures.Fragments(client)[0][6..38];
        using var transport = new UnitTestScriptedStream(client);
        var server = new TlsSrpServer(UnitTestSrpClient.Accounts(), new UnitTestRepeatingRandomNumberGenerator(UnitTestSrpClient.ServerRandomPattern), certificate);

        await Assert.ThrowsExactlyAsync<TlsAlertException>(() => server.AuthenticateAsync(transport, CancellationToken.None));

        var written = transport.Written.ToArray();
        var flight = UnitTestFixtures.Fragments(written)[0];
        var messages = UnitTestCertificates.Messages(flight);
        CollectionAssert.AreEqual(
            new[] { TlsHandshakeType.ServerHello, TlsHandshakeType.Certificate, TlsHandshakeType.ServerKeyExchange, TlsHandshakeType.ServerHelloDone },
            messages.Select(message => message.Type).ToArray());
        CollectionAssert.AreEqual(Convert.FromHexString("000010" + "000005" + "3003020101" + "000005" + "3003020102"), messages[1].Body);
        var parameters = UnitTestSrpClient.ExpectedServerKeyExchange()[4..];
        CollectionAssert.AreEqual(parameters, messages[2].Body[..parameters.Length]);
        return (messages, [.. clientRandom, .. UnitTestSrpClient.ServerRandomPattern, .. parameters]);
    }

    private static byte[] SignatureOf(byte[] keyExchangeBody, ushort scheme)
    {
        var parameters = UnitTestSrpClient.ExpectedServerKeyExchange().Length - 4;
        var digitallySigned = keyExchangeBody[parameters..];
        Assert.AreEqual(scheme, (ushort)((digitallySigned[0] << 8) | digitallySigned[1]));
        var length = (digitallySigned[2] << 8) | digitallySigned[3];
        Assert.AreEqual(length, digitallySigned.Length - 4);
        return digitallySigned[4..];
    }

    [TestMethod]
    public async Task AuthenticateAsync_NullTransport_Throws()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UnitTestSrpClient.Server().AuthenticateAsync(null!, CancellationToken.None));
    }
}
