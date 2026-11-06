namespace Surl.Tls13;

[TestClass]
public sealed class ClientHelloTests
{
    [TestMethod]
    public void Read_Rfc8448Section3ClientHello_ReadsEveryField()
    {
        var expectedRandom = Rfc8448.ClientHello[6..38];

        var hello = ClientHello.Read(Rfc8448.ClientHello);

        CollectionAssert.AreEqual(expectedRandom, hello.Random);
        Assert.IsEmpty(hello.LegacySessionId);
        CollectionAssert.AreEqual(
            new[] { TlsCipherSuite.Aes128GcmSha256, TlsCipherSuite.ChaCha20Poly1305Sha256, TlsCipherSuite.Aes256GcmSha384 },
            hello.CipherSuites.ToArray());
        CollectionAssert.AreEqual(
            new ushort[] { 0x0000, 0xFF01, 0x000A, 0x0023, 0x0033, 0x002B, 0x000D, 0x002D, 0x001C },
            hello.Extensions.Select(extension => (ushort)extension.Type).ToArray());
    }

    [TestMethod]
    public void Read_Rfc8448Section3ClientHello_DecodesItsExtensions()
    {
        var hello = ClientHello.Read(Rfc8448.ClientHello);

        Assert.AreEqual("server", hello.ServerName);
        CollectionAssert.AreEqual(new ushort[] { 0x0304 }, hello.SupportedVersions!.ToArray());
        CollectionAssert.AreEqual(
            new ushort[] { 0x001D, 0x0017, 0x0018, 0x0019, 0x0100, 0x0101, 0x0102, 0x0103, 0x0104 },
            hello.SupportedGroups!.Select(group => (ushort)group).ToArray());
        CollectionAssert.AreEqual(
            new ushort[]
            {
                0x0403, 0x0503, 0x0603, 0x0203, 0x0804, 0x0805, 0x0806, 0x0401,
                0x0501, 0x0601, 0x0201, 0x0402, 0x0502, 0x0602, 0x0202,
            },
            hello.SignatureAlgorithms!.Select(scheme => (ushort)scheme).ToArray());
        CollectionAssert.AreEqual(new byte[] { 1 }, hello.PskKeyExchangeModes!.ToArray());
        Assert.HasCount(1, hello.KeyShares!);
        Assert.AreEqual(TlsNamedGroup.X25519, hello.KeyShares![0].Group);
        CollectionAssert.AreEqual(
            Convert.FromHexString("99381de560e4bd43d23d8e435a7dbafeb3c06e51c13cae4d5413691e529aaf2c"),
            hello.KeyShares[0].KeyExchange.ToArray());
        Assert.IsNull(hello.ApplicationProtocols);
        Assert.IsNull(hello.QuicTransportParameters);
    }

    [TestMethod]
    [DataRow("static-8.21.0-win.bin", "localhost")]
    [DataRow("curlse-8.22.0_2-win.bin", "localhost")]
    [DataRow("static-8.21.0-linux.bin", null)]
    public void Read_RecordedUpstreamCurlClientHello_ParsesAndOffersH3(string fixture, string? serverName)
    {
        var message = UnitTestFixtures.Read(fixture);

        var hello = ClientHello.Read(message);

        CollectionAssert.Contains(hello.ApplicationProtocols!.ToArray(), "h3");
        CollectionAssert.AreEqual(new[] { "h3", "h3-29" }, hello.ApplicationProtocols!.ToArray());
        CollectionAssert.AreEqual(new ushort[] { 0x0304 }, hello.SupportedVersions!.ToArray());
        Assert.AreEqual(serverName, hello.ServerName);
        Assert.IsNotNull(hello.QuicTransportParameters);
        Assert.IsTrue(hello.KeyShares!.Any(share => share.Group == TlsNamedGroup.X25519 && share.KeyExchange.Length == 32));
        Assert.AreEqual(TlsCipherSuite.Aes256GcmSha384, hello.CipherSuites[0]);
    }

    [TestMethod]
    public void Read_HandshakeLengthOverrunsTheMessage_FailsWithDecodeError()
    {
        var truncated = Rfc8448.ClientHello[..^1];

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(truncated));

        Assert.AreEqual(TlsAlert.DecodeError, exception.Alert);
    }

    [TestMethod]
    public void Read_ExtensionLengthOverrunsTheBlock_FailsWithDecodeError()
    {
        var message = (byte[])Rfc8448.ClientHello.Clone();
        message[^3] = 0x10;

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.DecodeError, exception.Alert);
    }

    [TestMethod]
    public void Read_BytesAfterTheMessage_FailsWithDecodeError()
    {
        byte[] message = [.. Rfc8448.ClientHello, 0x00];

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.DecodeError, exception.Alert);
    }

    [TestMethod]
    public void Read_BytesAfterTheExtensions_FailsWithDecodeError()
    {
        var message = new UnitTestClientHelloBytes { Trailing = [0x00] }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.DecodeError, exception.Alert);
    }

    [TestMethod]
    public void Read_DuplicateExtension_FailsWithIllegalParameter()
    {
        var message = new UnitTestClientHelloBytes
        {
            Extensions = [(43, [0x02, 0x03, 0x04]), (43, [0x02, 0x03, 0x04])],
        }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.IllegalParameter, exception.Alert);
    }

    [TestMethod]
    [DataRow((byte)0x03, (byte)0x01)]
    [DataRow((byte)0x03, (byte)0x04)]
    [DataRow((byte)0x02, (byte)0x00)]
    public void Read_LegacyVersionIsNot0303_FailsWithProtocolVersion(byte major, byte minor)
    {
        var message = new UnitTestClientHelloBytes { LegacyVersion = [major, minor] }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.ProtocolVersion, exception.Alert);
    }

    [TestMethod]
    public void Read_EmptyCipherSuiteList_FailsWithDecodeError()
    {
        var message = new UnitTestClientHelloBytes { CipherSuitesVector = [0x00, 0x00] }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.DecodeError, exception.Alert);
    }

    [TestMethod]
    public void Read_OddLengthCipherSuiteList_FailsWithDecodeError()
    {
        var message = new UnitTestClientHelloBytes { CipherSuitesVector = [0x00, 0x03, 0x13, 0x01, 0x13] }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.DecodeError, exception.Alert);
    }

    [TestMethod]
    public void Read_NotAClientHello_FailsWithUnexpectedMessage()
    {
        var message = new UnitTestClientHelloBytes { HandshakeType = 2 }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.UnexpectedMessage, exception.Alert);
    }

    [TestMethod]
    public void Read_SessionIdLongerThan32Bytes_FailsWithDecodeError()
    {
        var message = new UnitTestClientHelloBytes { SessionIdVector = [33, .. new byte[33]] }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.DecodeError, exception.Alert);
    }

    [TestMethod]
    public void Read_SessionIdOf32Bytes_KeepsIt()
    {
        var sessionId = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var message = new UnitTestClientHelloBytes { SessionIdVector = [32, .. sessionId] }.Write();

        var hello = ClientHello.Read(message);

        CollectionAssert.AreEqual(sessionId, hello.LegacySessionId);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x01, 0x01 })]
    [DataRow(new byte[] { 0x02, 0x00, 0x01 })]
    public void Read_CompressionOtherThanNull_FailsWithIllegalParameter(byte[] compressionVector)
    {
        var message = new UnitTestClientHelloBytes { CompressionVector = compressionVector }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.IllegalParameter, exception.Alert);
    }

    [TestMethod]
    public void Read_EmptyCompressionList_FailsWithDecodeError()
    {
        var message = new UnitTestClientHelloBytes { CompressionVector = [0x00] }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.DecodeError, exception.Alert);
    }

    [TestMethod]
    public void Read_NoExtensionBlock_HasNoExtensionsAndNothingDecoded()
    {
        var message = new UnitTestClientHelloBytes { OmitExtensions = true }.Write();

        var hello = ClientHello.Read(message);

        Assert.IsEmpty(hello.Extensions);
        Assert.IsNull(hello.ServerName);
        Assert.IsNull(hello.SupportedVersions);
        Assert.IsNull(hello.SupportedGroups);
        Assert.IsNull(hello.SignatureAlgorithms);
        Assert.IsNull(hello.ApplicationProtocols);
        Assert.IsNull(hello.PskKeyExchangeModes);
        Assert.IsNull(hello.KeyShares);
        Assert.IsNull(hello.QuicTransportParameters);
    }

    [TestMethod]
    public void Read_PreSharedKeyNotLast_FailsWithIllegalParameter()
    {
        var message = new UnitTestClientHelloBytes
        {
            Extensions = [(41, [0x00]), (43, [0x02, 0x03, 0x04])],
        }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.IllegalParameter, exception.Alert);
    }

    [TestMethod]
    public void Read_PreSharedKeyLast_KeepsItUndecoded()
    {
        var message = new UnitTestClientHelloBytes
        {
            Extensions = [(43, [0x02, 0x03, 0x04]), (41, [0x00])],
        }.Write();

        var hello = ClientHello.Read(message);

        Assert.AreEqual(TlsExtensionType.PreSharedKey, hello.Extensions[^1].Type);
    }

    [TestMethod]
    public void Read_KeyShareOffersAGroupTwice_FailsWithIllegalParameter()
    {
        var message = new UnitTestClientHelloBytes
        {
            Extensions = [(51, [0x00, 0x0A, 0x00, 0x1D, 0x00, 0x01, 0xAA, 0x00, 0x1D, 0x00, 0x01, 0xBB])],
        }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.IllegalParameter, exception.Alert);
    }

    [TestMethod]
    [DataRow((ushort)0, new byte[] { 0x00, 0x05, 0x00, 0x00, 0x02, 0x61, 0x62, 0xFF })]
    [DataRow((ushort)43, new byte[] { 0x02, 0x03, 0x04, 0xFF })]
    [DataRow((ushort)10, new byte[] { 0x00, 0x02, 0x00, 0x1D, 0xFF })]
    [DataRow((ushort)16, new byte[] { 0x00, 0x03, 0x02, 0x68, 0x33, 0xFF })]
    [DataRow((ushort)45, new byte[] { 0x01, 0x01, 0xFF })]
    [DataRow((ushort)51, new byte[] { 0x00, 0x05, 0x00, 0x1D, 0x00, 0x01, 0xAA, 0xFF })]
    [DataRow((ushort)16, new byte[] { 0x00, 0x02, 0x00, 0x00 })]
    [DataRow((ushort)16, new byte[] { 0x00, 0x00 })]
    [DataRow((ushort)43, new byte[] { 0x00 })]
    [DataRow((ushort)0, new byte[] { 0x00, 0x03, 0x00, 0x00, 0x00 })]
    public void Read_MalformedExtensionData_FailsWithDecodeError(ushort type, byte[] data)
    {
        var message = new UnitTestClientHelloBytes { Extensions = [(type, data)] }.Write();

        var exception = Assert.ThrowsExactly<TlsAlertException>(() => ClientHello.Read(message));

        Assert.AreEqual(TlsAlert.DecodeError, exception.Alert);
    }

    [TestMethod]
    public void Read_ServerNameWithAnotherNameTypeFirst_KeepsTheFirstHostName()
    {
        byte[] data = [0x00, 0x0C, 0x01, 0x00, 0x01, 0x78, 0x00, 0x00, 0x01, 0x61, 0x00, 0x00, 0x01, 0x62];
        data[1] = (byte)(data.Length - 2);
        var message = new UnitTestClientHelloBytes { Extensions = [(0, data)] }.Write();

        var hello = ClientHello.Read(message);

        Assert.AreEqual("a", hello.ServerName);
    }

    [TestMethod]
    public void Read_ServerNameWithNoHostName_HasNoServerName()
    {
        var message = new UnitTestClientHelloBytes { Extensions = [(0, [0x00, 0x04, 0x01, 0x00, 0x01, 0x78])] }.Write();

        var hello = ClientHello.Read(message);

        Assert.IsNull(hello.ServerName);
    }
}
