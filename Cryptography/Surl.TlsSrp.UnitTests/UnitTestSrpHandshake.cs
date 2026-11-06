using System.Security.Cryptography;

namespace Surl.TlsSrp;

/// <summary>
/// One whole client side of a TLS-SRP handshake against <see cref="UnitTestSrpClient.Server"/>,
/// computed from the RFCs: the client's records, the server flight it expects, the master secret
/// and keys both sides should derive, and the client's write protection for application data after
/// its <c>Finished</c>.
/// </summary>
internal sealed class UnitTestSrpHandshake
{
    public UnitTestSrpHandshake(
        string userName = UnitTestSrpClient.UserName,
        string password = UnitTestSrpClient.Password,
        bool extendedMasterSecret = false,
        bool encryptThenMac = false,
        bool alpn = false,
        ushort suite = 0xC020)
    {
        EncryptThenMac = encryptThenMac;
        Suite = (TlsSrpCipherSuite)suite;
        var extensions = new List<byte[]> { UnitTestSrpClient.SrpExtension(userName) };
        var serverExtensions = new List<byte[]>();
        if (extendedMasterSecret)
        {
            extensions.Add(UnitTestSrpClient.Extension(TlsExtensionType.ExtendedMasterSecret, []));
            serverExtensions.Add(UnitTestSrpClient.Extension(TlsExtensionType.ExtendedMasterSecret, []));
        }

        if (encryptThenMac)
        {
            extensions.Add(UnitTestSrpClient.Extension(TlsExtensionType.EncryptThenMac, []));
            serverExtensions.Add(UnitTestSrpClient.Extension(TlsExtensionType.EncryptThenMac, []));
        }

        if (alpn)
        {
            extensions.Add(UnitTestSrpClient.Extension(TlsExtensionType.ApplicationLayerProtocolNegotiation, UnitTestSrpClient.Vector16([2, .. "h2"u8, 8, .. "http/1.1"u8])));
            serverExtensions.Add(UnitTestSrpClient.Extension(TlsExtensionType.ApplicationLayerProtocolNegotiation, UnitTestSrpClient.Vector16([8, .. "http/1.1"u8])));
        }

        Hello = UnitTestSrpClient.Handshake(
            TlsHandshakeType.ClientHello, UnitTestSrpClient.ClientHelloBody(0x0303, [0x00FF, suite], [0], [.. extensions]));
        ServerFlight =
        [
            .. UnitTestSrpClient.ExpectedServerHello(suite, [UnitTestSrpClient.Extension(TlsExtensionType.RenegotiationInfo, [0]), .. serverExtensions]),
            .. UnitTestSrpClient.ExpectedServerKeyExchange(),
            .. UnitTestSrpClient.Handshake(TlsHandshakeType.ServerHelloDone, []),
        ];
        KeyExchange = UnitTestSrpClient.ClientKeyExchange();
        byte[] transcript = [.. Hello, .. ServerFlight, .. KeyExchange];
        var premaster = UnitTestSrpClient.ClientPremasterSecret(userName, password);
        MasterSecret = extendedMasterSecret
            ? TlsPseudorandomFunction.ExtendedMasterSecret(premaster, SHA256.HashData(transcript))
            : TlsPseudorandomFunction.MasterSecret(premaster, UnitTestSrpClient.ClientRandom, UnitTestSrpClient.ServerRandomPattern);
        Keys = TlsKeyBlock.Derive(Suite, MasterSecret, UnitTestSrpClient.ClientRandom, UnitTestSrpClient.ServerRandomPattern);
        ClientFinished = UnitTestSrpClient.Handshake(
            TlsHandshakeType.Finished, TlsPseudorandomFunction.ClientFinishedVerifyData(MasterSecret, SHA256.HashData(transcript)));
        ServerFinished = UnitTestSrpClient.Handshake(
            TlsHandshakeType.Finished, TlsPseudorandomFunction.ServerFinishedVerifyData(MasterSecret, SHA256.HashData([.. transcript, .. ClientFinished])));
        ClientWrite = UnitTestSrpClient.Protection(Keys.Cipher, Keys.ClientKey, Keys.ClientMacKey, encryptThenMac);
    }

    public bool EncryptThenMac { get; }

    public TlsSrpCipherSuite Suite { get; }

    public byte[] Hello { get; }

    public byte[] ServerFlight { get; }

    public byte[] KeyExchange { get; }

    public byte[] MasterSecret { get; }

    public TlsKeyBlock Keys { get; }

    public byte[] ClientFinished { get; }

    public byte[] ServerFinished { get; }

    public TlsCbcRecordProtection ClientWrite { get; }

    public static byte[] ChangeCipherSpecRecord => UnitTestSrpClient.Record(TlsContentType.ChangeCipherSpec, [1]);

    /// <summary>
    /// The client's records: hello, ClientKeyExchange, <c>ChangeCipherSpec</c>, then
    /// <paramref name="finished"/> (the right <c>Finished</c> by default) protected, then
    /// <paramref name="after"/> each protected as application data.
    /// </summary>
    public byte[] ClientBytes(byte[]? finished = null, params byte[][] after)
    {
        var bytes = new List<byte>();
        bytes.AddRange(UnitTestSrpClient.Record(TlsContentType.Handshake, Hello, 0x0301));
        bytes.AddRange(UnitTestSrpClient.Record(TlsContentType.Handshake, KeyExchange));
        bytes.AddRange(ChangeCipherSpecRecord);
        bytes.AddRange(UnitTestSrpClient.ProtectedRecord(ClientWrite, TlsContentType.Handshake, finished ?? ClientFinished));
        foreach (var data in after)
        {
            bytes.AddRange(UnitTestSrpClient.ProtectedRecord(ClientWrite, TlsContentType.ApplicationData, data));
        }

        return [.. bytes];
    }

    /// <summary>A reader of what the server wrote after its <c>ChangeCipherSpec</c>, through the server's write keys.</summary>
    public TlsRecordReader ServerRecordsAfterChangeCipherSpec(byte[] written)
    {
        var skipped = TlsRecordFormat.HeaderLength + ServerFlight.Length + ChangeCipherSpecRecord.Length;
        var reader = new TlsRecordReader(new MemoryStream(written[skipped..]));
        reader.ChangeProtection(Keys.ServerWriteProtection(EncryptThenMac, RandomNumberGenerator.Create()));
        return reader;
    }
}
