using System.Security.Cryptography;
using Surl.Cryptography.Curve25519;

namespace Surl.Dtls;

/// <summary>
/// A DTLS 1.2 client written in the test project from RFC 6347 section 4.2 and RFC 5246 section 7,
/// enough to drive <see cref="DtlsServerSession"/> through the cookie exchange and an ECDHE
/// certificate handshake, then exchange application data. It reacts to each datagram the server
/// sends and returns the datagrams it answers with; its knobs make it misbehave the ways the tests need.
/// </summary>
internal sealed class UnitTestDtlsClient
{
    public const ushort DefaultSuite = 0xC02B;

    private readonly DtlsRecordReader epoch0Reader = new(epoch: 0, protection: null);
    private readonly List<byte> transcript = [];
    private readonly DtlsHandshakeReassembler serverMessages = new(1 << 20);
    private readonly DtlsRecordWriter epoch0Writer;
    private DtlsRecordWriter? epoch1Writer;
    private DtlsRecordReader? epoch1Reader;
    private DtlsCipherSuite? suite;
    private byte[] cookie;
    private byte[] serverRandom = [];
    private byte[] masterSecret = [];
    private DtlsEcdheServerKeyExchange? serverKeyExchange;
    private bool usesExtendedMasterSecret;
    private List<byte[]>? finalFlight;
    private List<List<(DtlsContentType Type, bool Protected, byte[] Plaintext)>>? finalFlightPlaintexts;

    public UnitTestDtlsClient(long initialRecordSequenceNumber = 0, byte[]? initialCookie = null)
    {
        epoch0Writer = new DtlsRecordWriter(epoch: 0, protection: null, initialRecordSequenceNumber);
        cookie = initialCookie ?? [];
    }

    public byte[] Random { get; } = RandomNumberGenerator.GetBytes(DtlsClientHello.RandomLength);

    public ushort[] CipherSuites { get; init; } = [DefaultSuite];

    public DtlsNamedGroup Group { get; init; } = DtlsNamedGroup.Secp256r1;

    public ushort[] SignatureSchemes { get; init; } = [0x0403, 0x0503, 0x0804, 0x0805, 0x0401, 0x0501];

    public bool OffersExtendedMasterSecret { get; init; } = true;

    /// <summary>Whether the client answers a <c>HelloVerifyRequest</c> with a second hello.</summary>
    public bool AnswersHelloVerifyRequest { get; init; } = true;

    /// <summary>Whether the client sends its final flight at all.</summary>
    public bool SendsFinalFlight { get; init; } = true;

    /// <summary>Whether the client's <c>Finished</c> carries wrong <c>verify_data</c>.</summary>
    public bool CorruptsFinished { get; init; }

    /// <summary>What the client's <c>ChangeCipherSpec</c> carries; RFC 5246 section 7.1 says the byte 1.</summary>
    public byte[] ChangeCipherSpecBody { get; init; } = [1];

    /// <summary>Whether the final flight goes as two datagrams: the epoch 0 records, then <c>Finished</c>.</summary>
    public bool SplitsFinalFlight { get; init; }

    /// <summary>Datagrams the client sends before its final flight, built once the server's flight is in.</summary>
    public Func<UnitTestDtlsClient, IEnumerable<byte[]>>? BeforeFinalFlight { get; init; }

    /// <summary>Replaces the final flight, built once the server's flight is in.</summary>
    public Func<UnitTestDtlsClient, IEnumerable<byte[]>>? ReplacesFinalFlight { get; init; }

    public ushort NextMessageSeq { get; private set; }

    public byte[] LastHelloDatagram { get; private set; } = [];

    public DtlsServerHello? ServerHello { get; private set; }

    public DtlsHelloVerifyRequest? HelloVerifyRequest { get; private set; }

    public bool IsEstablished { get; private set; }

    public List<byte[]> ReceivedApplicationData { get; } = [];

    public List<DtlsAlert> ReceivedAlerts { get; } = [];

    public int ServerFlightsSeenAgain { get; private set; }

    public DtlsClientHello BuildHello() => new(
        DtlsRecordHeader.Dtls12Version, Random, [], cookie, CipherSuites, [0], BuildExtensions());

    public byte[] FirstHelloDatagram() => HelloDatagram();

    /// <summary>A plaintext epoch 0 record of the client's.</summary>
    public byte[] PlaintextRecord(DtlsContentType contentType, byte[] plaintext) => epoch0Writer.Seal(contentType, plaintext);

    /// <summary>A plaintext epoch 0 handshake record carrying a whole message with the next <c>message_seq</c>.</summary>
    public byte[] HandshakeRecord(DtlsHandshakeType type, byte[] body) =>
        PlaintextRecord(DtlsContentType.Handshake, new DtlsHandshakeMessage(type, NextMessageSeq++, body).ToUnfragmentedBytes());

    /// <summary>An epoch 1 record under the client's keys, once the final flight is built.</summary>
    public byte[] ProtectedRecord(DtlsContentType contentType, byte[] plaintext) => epoch1Writer!.Seal(contentType, plaintext);

    /// <summary>
    /// The client's real final flight: <c>ClientKeyExchange</c>, <c>ChangeCipherSpec</c>,
    /// <c>Finished</c>. It is kept as plaintexts, so each time it is sent again its records carry
    /// fresh sequence numbers, as RFC 6347 section 4.2.4 requires.
    /// </summary>
    public List<byte[]> BuildFinalFlight()
    {
        var keyExchange = AddClientMessage(
            DtlsHandshakeType.ClientKeyExchange, new DtlsEcdheClientKeyExchange(AgreeAndDeriveKeys()).Write());
        var verifyData = DtlsPseudorandomFunction.ClientFinishedVerifyData(suite!.PrfHash, masterSecret, TranscriptHash());
        if (CorruptsFinished)
        {
            verifyData[0] ^= 0xFF;
        }

        var finished = AddClientMessage(DtlsHandshakeType.Finished, new DtlsFinished(verifyData).Write());
        List<(DtlsContentType, bool, byte[])> epoch0 =
            [(DtlsContentType.Handshake, false, keyExchange), (DtlsContentType.ChangeCipherSpec, false, ChangeCipherSpecBody)];
        List<(DtlsContentType, bool, byte[])> epoch1 = [(DtlsContentType.Handshake, true, finished)];
        finalFlightPlaintexts = SplitsFinalFlight ? [epoch0, epoch1] : [[.. epoch0, .. epoch1]];
        return SealFinalFlight();
    }

    private List<byte[]> SealFinalFlight() =>
        [.. finalFlightPlaintexts!.Select(datagram => datagram
            .SelectMany(record => record.Protected ? ProtectedRecord(record.Type, record.Plaintext) : PlaintextRecord(record.Type, record.Plaintext))
            .ToArray())];

    /// <summary>Takes in one datagram from the server and returns the client's answers.</summary>
    public List<byte[]> OnServerDatagram(byte[] datagram)
    {
        // Records are read one at a time, so a ChangeCipherSpec starts epoch 1 reads before the
        // Finished behind it in the same datagram is read.
        var answers = new List<byte[]>();
        var offset = 0;
        while (DtlsRecordHeader.TryRead(datagram.AsSpan(offset), out var header))
        {
            var length = DtlsRecordHeader.Size + header.Length;
            var reader = header.Epoch == 0 ? epoch0Reader : epoch1Reader;
            foreach (var record in reader?.ReadDatagram(datagram.AsSpan(offset, length)).Records ?? [])
            {
                answers.AddRange(OnServerRecord(record));
            }

            offset += length;
        }

        return answers;
    }

    private IEnumerable<byte[]> OnServerRecord(DtlsRecord record)
    {
        switch (record.Header.ContentType)
        {
            case DtlsContentType.Handshake:
                return OnServerHandshakeFragments(record.Plaintext);
            case DtlsContentType.ChangeCipherSpec:
                epoch1Reader = new DtlsRecordReader(epoch: 1, ServerProtection());
                return [];
            case DtlsContentType.Alert:
                DtlsAlert.TryRead(record.Plaintext, out var alert);
                ReceivedAlerts.Add(alert);
                return [];
            default:
                ReceivedApplicationData.Add(record.Plaintext);
                return [];
        }
    }

    private List<byte[]> OnServerHandshakeFragments(byte[] plaintext)
    {
        var answers = new List<byte[]>();
        var fragments = plaintext.AsSpan();
        var seenAgain = false;
        while (!fragments.IsEmpty)
        {
            seenAgain |= serverMessages.Accept(fragments, out var consumed) == DtlsHandshakeFragmentOutcome.Stale;
            fragments = fragments[consumed..];
        }

        while (serverMessages.TryTakeNext(out var message))
        {
            answers.AddRange(OnServerMessage(message));
        }

        if (seenAgain && finalFlight is not null && !IsEstablished)
        {
            ServerFlightsSeenAgain++;
            answers.AddRange(finalFlightPlaintexts is null ? finalFlight : SealFinalFlight());
        }

        return answers;
    }

    private IEnumerable<byte[]> OnServerMessage(DtlsHandshakeMessage message)
    {
        switch (message.MessageType)
        {
            case DtlsHandshakeType.HelloVerifyRequest:
                HelloVerifyRequest = DtlsHelloVerifyRequest.Read(message.Body);
                cookie = HelloVerifyRequest.Cookie;
                return AnswersHelloVerifyRequest ? [HelloDatagram()] : [];
            case DtlsHandshakeType.ServerHello:
                ServerHello = DtlsServerHello.Read(message.Body);
                serverRandom = ServerHello.Random;
                suite = DtlsCipherSuite.CertificateSuitesInServerOrder.Single(candidate => candidate.Code == ServerHello.CipherSuite);
                usesExtendedMasterSecret = ServerHello.Extensions.Any(extension => extension.Type == DtlsExtensionType.ExtendedMasterSecret);
                break;
            case DtlsHandshakeType.ServerKeyExchange:
                serverKeyExchange = DtlsEcdheServerKeyExchange.Read(message.Body);
                break;
            case DtlsHandshakeType.Finished:
                var expected = DtlsPseudorandomFunction.ServerFinishedVerifyData(suite!.PrfHash, masterSecret, TranscriptHash());
                IsEstablished = expected.AsSpan().SequenceEqual(DtlsFinished.Read(message.Body).VerifyData);
                return [];
        }

        transcript.AddRange(message.ToUnfragmentedBytes());
        return message.MessageType == DtlsHandshakeType.ServerHelloDone ? StartFinalFlight() : [];
    }

    private List<byte[]> StartFinalFlight()
    {
        if (!SendsFinalFlight)
        {
            return [];
        }

        var datagrams = BeforeFinalFlight?.Invoke(this).ToList() ?? [];
        finalFlight = ReplacesFinalFlight?.Invoke(this).ToList() ?? BuildFinalFlight();
        datagrams.AddRange(finalFlight);
        return datagrams;
    }

    private byte[] HelloDatagram()
    {
        var message = new DtlsHandshakeMessage(DtlsHandshakeType.ClientHello, NextMessageSeq++, BuildHello().Write());
        transcript.Clear();
        transcript.AddRange(message.ToUnfragmentedBytes());
        LastHelloDatagram = PlaintextRecord(DtlsContentType.Handshake, message.ToUnfragmentedBytes());
        return LastHelloDatagram;
    }

    private List<DtlsExtension> BuildExtensions()
    {
        var extensions = new List<DtlsExtension>
        {
            new(DtlsExtensionType.SupportedGroups, UInt16List([(ushort)Group])),
            new(DtlsExtensionType.EcPointFormats, [1, 0]),
            new(DtlsExtensionType.SignatureAlgorithms, UInt16List(SignatureSchemes)),
            new(DtlsExtensionType.RenegotiationInfo, [0]),
        };
        if (OffersExtendedMasterSecret)
        {
            extensions.Add(new DtlsExtension(DtlsExtensionType.ExtendedMasterSecret, []));
        }

        return extensions;
    }

    public static byte[] UInt16List(IReadOnlyList<ushort> values)
    {
        var bytes = new byte[2 + (2 * values.Count)];
        bytes[0] = (byte)((2 * values.Count) >> 8);
        bytes[1] = (byte)(2 * values.Count);
        for (var index = 0; index < values.Count; index++)
        {
            bytes[2 + (2 * index)] = (byte)(values[index] >> 8);
            bytes[3 + (2 * index)] = (byte)values[index];
        }

        return bytes;
    }

    private byte[] AddClientMessage(DtlsHandshakeType type, byte[] body)
    {
        var bytes = new DtlsHandshakeMessage(type, NextMessageSeq++, body).ToUnfragmentedBytes();
        transcript.AddRange(bytes);
        return bytes;
    }

    /// <summary>
    /// Makes the client's ECDHE key, derives the premaster, master secret and key block, and starts
    /// epoch 1 writes; the transcript hash for the extended master secret covers the
    /// <c>ClientKeyExchange</c>, so the master secret is derived once it is added.
    /// </summary>
    private byte[] AgreeAndDeriveKeys()
    {
        var (publicPoint, preMasterSecret) = Agree((DtlsNamedGroup)serverKeyExchange!.NamedGroup, serverKeyExchange.PublicPoint);
        var keyExchange = new DtlsHandshakeMessage(
            DtlsHandshakeType.ClientKeyExchange, NextMessageSeq, new DtlsEcdheClientKeyExchange(publicPoint).Write());
        byte[] sessionTranscript = [.. transcript, .. keyExchange.ToUnfragmentedBytes()];
        masterSecret = usesExtendedMasterSecret
            ? DtlsPseudorandomFunction.ExtendedMasterSecret(suite!.PrfHash, preMasterSecret, CryptographicOperations.HashData(suite.PrfHash, sessionTranscript))
            : DtlsPseudorandomFunction.MasterSecret(suite!.PrfHash, preMasterSecret, Random, serverRandom);
        var keyBlock = KeyBlock();
        var keyLength = suite.KeyLength;
        epoch1Writer = new DtlsRecordWriter(
            epoch: 1, new DtlsRecordProtection(suite.Aead, keyBlock.AsSpan(0, keyLength), keyBlock.AsSpan(2 * keyLength, 4)));
        return publicPoint;
    }

    private DtlsRecordProtection ServerProtection()
    {
        var keyBlock = KeyBlock();
        var keyLength = suite!.KeyLength;
        return new DtlsRecordProtection(suite.Aead, keyBlock.AsSpan(keyLength, keyLength), keyBlock.AsSpan((2 * keyLength) + 4, 4));
    }

    private byte[] KeyBlock() =>
        DtlsPseudorandomFunction.KeyBlock(suite!.PrfHash, masterSecret, Random, serverRandom, suite.KeyBlockLength);

    private byte[] TranscriptHash() => CryptographicOperations.HashData(suite!.PrfHash, [.. transcript]);

    private static (byte[] PublicPoint, byte[] SharedSecret) Agree(DtlsNamedGroup group, byte[] serverPoint)
    {
        if (group == DtlsNamedGroup.X25519)
        {
            var privateKey = RandomNumberGenerator.GetBytes(X25519.KeySize);
            var publicKey = new byte[X25519.KeySize];
            var shared = new byte[X25519.KeySize];
            X25519.ComputePublicKey(privateKey, publicKey);
            X25519.ScalarMultiply(privateKey, serverPoint, shared);
            return (publicKey, shared);
        }

        var curve = group == DtlsNamedGroup.Secp256r1 ? ECCurve.NamedCurves.nistP256 : ECCurve.NamedCurves.nistP384;
        var length = (serverPoint.Length - 1) / 2;
        using var client = ECDiffieHellman.Create(curve);
        using var server = ECDiffieHellman.Create(new ECParameters
        {
            Curve = curve,
            Q = new ECPoint { X = serverPoint[1..(1 + length)], Y = serverPoint[(1 + length)..] },
        });
        var q = client.ExportParameters(false).Q;
        return ([0x04, .. q.X!, .. q.Y!], client.DeriveRawSecretAgreement(server.PublicKey));
    }
}
