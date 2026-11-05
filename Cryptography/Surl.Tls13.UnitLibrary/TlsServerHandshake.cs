using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Surl.Tls13;

/// <summary>
/// The server side of a TLS 1.3 handshake for QUIC (RFC 8446 section 4, RFC 9001 section 4) with
/// the parameters of ADR-0082 decision 3. QUIC hands it the client's handshake bytes as they
/// arrive at each encryption level; it returns the server's handshake bytes for each level and
/// the traffic secrets as they become known, and checks the client's Finished. When
/// <see cref="TlsServerHandshakeOptions.TrustsClientCertificateChain"/> is set it sends a
/// CertificateRequest and requires and verifies the client's Certificate and CertificateVerify
/// first. There is no EndOfEarlyData, no ChangeCipherSpec, no session ticket and no
/// post-handshake authentication.
/// </summary>
/// <remarks>
/// An alert ends the handshake: <see cref="Receive"/> throws <see cref="TlsAlertException"/>, and
/// every later call throws <see cref="InvalidOperationException"/>. Randomness - the server random,
/// then an x25519 private key - is drawn from the injected generator, in that order.
/// </remarks>
public sealed class TlsServerHandshake : IDisposable
{
    /// <summary>The largest handshake message accepted, in bytes; a longer one is <see cref="TlsAlert.IllegalParameter"/>.</summary>
    public const int MaximumMessageLength = 1 << 17;

    private const int HandshakeHeaderLength = 4;
    private const int ServerRandomLength = 32;

    private readonly TlsServerHandshakeOptions options;
    private readonly RandomNumberGenerator random;
    private readonly TlsSignatureScheme keyScheme;
    private readonly List<byte> initialBytes = [];
    private readonly List<byte> handshakeBytes = [];
    private State state = State.AwaitingClientHello;
    private TlsTranscript? transcript;
    private TlsServerNegotiation? negotiation;
    private TlsNamedGroup retryGroup;
    private TlsKeySchedule? keySchedule;
    private byte[] clientHandshakeTrafficSecret = [];
    private X509Certificate2? clientCertificate;
    private IReadOnlyList<ReadOnlyMemory<byte>>? receivedClientCertificateChain;

    /// <summary>Starts a handshake that awaits the client's ClientHello.</summary>
    /// <param name="options">The certificate, key, ALPN protocols and extensions the server serves.</param>
    /// <param name="random">The source of the server random and the x25519 private key.</param>
    /// <exception cref="ArgumentException">The certificate chain is empty, or the key is neither RSA nor ECDSA on P-256, P-384 or P-521.</exception>
    public TlsServerHandshake(TlsServerHandshakeOptions options, RandomNumberGenerator random)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfZero(options.CertificateChain.Count, nameof(options));
        keyScheme = TlsCertificateSigner.SchemeOf(options.CertificateKey);
        this.options = options;
        this.random = random;
    }

    private enum State
    {
        AwaitingClientHello,
        AwaitingSecondClientHello,
        AwaitingClientCertificate,
        AwaitingClientCertificateVerify,
        AwaitingClientFinished,
        Complete,
        Failed,
    }

    /// <summary>Whether the client's Finished has been received and verified.</summary>
    public bool IsComplete => state == State.Complete;

    /// <summary>The ClientHello the handshake went on with (the second, after a HelloRetryRequest), or null before one is accepted.</summary>
    public ClientHello? ClientHello { get; private set; }

    /// <summary>The negotiated cipher suite, or null before a ServerHello or HelloRetryRequest is written.</summary>
    public TlsCipherSuite? CipherSuite => negotiation?.CipherSuite;

    /// <summary>The agreed ALPN protocol, or null when none is agreed (yet).</summary>
    public string? ApplicationProtocol => negotiation?.ApplicationProtocol;

    /// <summary>The key exchange group of the ServerHello, or null before one is written.</summary>
    public TlsNamedGroup? KeyExchangeGroup { get; private set; }

    /// <summary>
    /// The client's certificate chain (DER, end-entity certificate first) once its Certificate is
    /// trusted and its CertificateVerify verifies, or null when none was requested (yet).
    /// </summary>
    public IReadOnlyList<ReadOnlyMemory<byte>>? ClientCertificateChain { get; private set; }

    /// <summary>Takes the client's handshake bytes received at <paramref name="level"/>, in order, and handles each whole message.</summary>
    /// <param name="level">The encryption level the bytes arrived at.</param>
    /// <param name="bytes">The next bytes of that level's handshake stream; a message may span calls.</param>
    /// <returns>The server's bytes per level and the secrets this call produced.</returns>
    /// <exception cref="TlsAlertException">The handshake ends with the alert it carries.</exception>
    /// <exception cref="InvalidOperationException">An earlier call ended the handshake with an alert.</exception>
    public TlsHandshakeOutput Receive(TlsEncryptionLevel level, ReadOnlySpan<byte> bytes)
    {
        if (state == State.Failed)
        {
            throw new InvalidOperationException("The handshake has already ended with an alert.");
        }

        try
        {
            return ReceiveMessages(level, bytes);
        }
        catch (TlsAlertException)
        {
            state = State.Failed;
            throw;
        }
    }

    /// <summary>Releases the transcript hash and the client's certificate.</summary>
    public void Dispose()
    {
        transcript?.Dispose();
        clientCertificate?.Dispose();
    }

    private TlsHandshakeOutput ReceiveMessages(TlsEncryptionLevel level, ReadOnlySpan<byte> bytes)
    {
        var buffer = level switch
        {
            TlsEncryptionLevel.Initial => initialBytes,
            TlsEncryptionLevel.Handshake => handshakeBytes,
            _ => throw new TlsAlertException(
                TlsAlert.UnexpectedMessage, "A QUIC client sends no handshake message at the 1-RTT level (RFC 9001 section 4.1.3)."),
        };
        buffer.AddRange(bytes);
        var flight = new Flight();
        while (TakeMessage(buffer) is { } message)
        {
            HandleMessage(level, message, flight);
        }

        return new TlsHandshakeOutput([.. flight.Initial], [.. flight.Handshake], flight.HandshakeSecrets, flight.ApplicationSecrets);
    }

    private static byte[]? TakeMessage(List<byte> buffer)
    {
        if (buffer.Count < HandshakeHeaderLength)
        {
            return null;
        }

        var length = (buffer[1] << 16) | (buffer[2] << 8) | buffer[3];
        if (length > MaximumMessageLength)
        {
            throw new TlsAlertException(
                TlsAlert.IllegalParameter, $"A {length}-byte handshake message exceeds the {MaximumMessageLength}-byte limit.");
        }

        if (buffer.Count < HandshakeHeaderLength + length)
        {
            return null;
        }

        var message = buffer.GetRange(0, HandshakeHeaderLength + length).ToArray();
        buffer.RemoveRange(0, message.Length);
        return message;
    }

    private void HandleMessage(TlsEncryptionLevel level, byte[] message, Flight flight)
    {
        switch (state, level)
        {
            case (State.AwaitingClientHello, TlsEncryptionLevel.Initial):
                ReceiveFirstClientHello(message, flight);
                break;
            case (State.AwaitingSecondClientHello, TlsEncryptionLevel.Initial):
                ReceiveSecondClientHello(message, flight);
                break;
            case (State.AwaitingClientCertificate, TlsEncryptionLevel.Handshake):
                ReceiveClientCertificate(message);
                break;
            case (State.AwaitingClientCertificateVerify, TlsEncryptionLevel.Handshake):
                ReceiveClientCertificateVerify(message);
                break;
            case (State.AwaitingClientFinished, TlsEncryptionLevel.Handshake):
                ReceiveClientFinished(message);
                break;
            default:
                throw new TlsAlertException(
                    TlsAlert.UnexpectedMessage,
                    $"Handshake message type {message[0]} at the {level} level is not expected now ({state}).");
        }
    }

    private void ReceiveFirstClientHello(byte[] message, Flight flight)
    {
        var hello = ClientHello.Read(message);
        var chosen = TlsServerNegotiation.Choose(hello, options, keyScheme);
        var share = TlsServerNegotiation.ChooseKeyShare(hello);
        negotiation = chosen;
        transcript = new TlsTranscript(TlsKeySchedule.HashAlgorithmOf(chosen.CipherSuite));
        transcript.Append(message);
        if (share is null)
        {
            SendHelloRetryRequest(hello, TlsServerNegotiation.ChooseRetryGroup(hello), flight);
            return;
        }

        SendServerFlight(hello, share, flight);
    }

    private void SendHelloRetryRequest(ClientHello hello, TlsNamedGroup group, Flight flight)
    {
        retryGroup = group;
        var retry = ServerHello.HelloRetryRequest(
            hello.LegacySessionId,
            negotiation!.CipherSuite,
            [TlsExtensions.KeyShareHelloRetryRequest(group), TlsExtensions.SupportedVersionsSelected(TlsExtensions.Tls13Version)])
            .Write();
        transcript!.ReplaceWithMessageHash();
        transcript.Append(retry);
        flight.Initial.AddRange(retry);
        state = State.AwaitingSecondClientHello;
    }

    private void ReceiveSecondClientHello(byte[] message, Flight flight)
    {
        var hello = ClientHello.Read(message);
        var chosen = TlsServerNegotiation.Choose(hello, options, keyScheme);
        if (chosen.CipherSuite != negotiation!.CipherSuite)
        {
            throw new TlsAlertException(
                TlsAlert.IllegalParameter, "The second ClientHello would change the cipher suite the HelloRetryRequest chose (RFC 8446 section 4.1.4).");
        }

        if (hello.KeyShares is not [{ } share] || share.Group != retryGroup)
        {
            throw new TlsAlertException(
                TlsAlert.IllegalParameter,
                $"The second ClientHello must send one key share, for group 0x{(ushort)retryGroup:X4} (RFC 8446 section 4.2.8).");
        }

        negotiation = chosen;
        transcript!.Append(message);
        SendServerFlight(hello, share, flight);
    }

    private void SendServerFlight(ClientHello hello, TlsKeyShareEntry clientShare, Flight flight)
    {
        var suite = negotiation!.CipherSuite;
        var serverRandom = new byte[ServerRandomLength];
        random.GetBytes(serverRandom);
        var agreement = TlsKeyExchange.Agree(clientShare, random);
        var serverHello = new ServerHello(
            serverRandom,
            hello.LegacySessionId,
            suite,
            [
                TlsExtensions.KeyShareServer(new TlsKeyShareEntry(clientShare.Group, agreement.ServerShare)),
                TlsExtensions.SupportedVersionsSelected(TlsExtensions.Tls13Version),
            ]).Write();
        ClientHello = hello;
        KeyExchangeGroup = clientShare.Group;
        AppendTo(flight.Initial, serverHello);

        keySchedule = new TlsKeySchedule(suite);
        var handshakeSecrets = keySchedule.DeriveHandshakeTrafficSecrets(agreement.SharedSecret, transcript!.CurrentHash());
        CryptographicOperations.ZeroMemory(agreement.SharedSecret);
        AppendTo(flight.Handshake, new EncryptedExtensions(EncryptedExtensionsList()).Write());
        var requestsClientCertificate = options.TrustsClientCertificateChain is not null;
        if (requestsClientCertificate)
        {
            AppendTo(
                flight.Handshake,
                new CertificateRequest(
                    ReadOnlyMemory<byte>.Empty,
                    [TlsExtensions.SignatureAlgorithms(TlsCertificateVerifier.ClientSchemes)]).Write());
        }

        AppendTo(
            flight.Handshake,
            new CertificateMessage(
                ReadOnlyMemory<byte>.Empty,
                [.. options.CertificateChain.Select(certificate => new TlsCertificateEntry(certificate, []))]).Write());
        AppendTo(flight.Handshake, TlsCertificateSigner.Sign(options.CertificateKey, transcript.CurrentHash()).Write());
        AppendTo(
            flight.Handshake,
            new Finished(keySchedule.ComputeFinishedVerifyData(handshakeSecrets.Server, transcript.CurrentHash())).Write());

        flight.HandshakeSecrets = handshakeSecrets;
        flight.ApplicationSecrets = keySchedule.DeriveApplicationTrafficSecrets(transcript.CurrentHash());
        clientHandshakeTrafficSecret = handshakeSecrets.Client;
        state = requestsClientCertificate ? State.AwaitingClientCertificate : State.AwaitingClientFinished;
    }

    private List<TlsExtension> EncryptedExtensionsList()
    {
        var extensions = new List<TlsExtension>(options.AdditionalEncryptedExtensions);
        if (negotiation!.ApplicationProtocol is { } protocol)
        {
            extensions.Add(TlsExtensions.ApplicationProtocolSelected(protocol));
        }

        if (options.QuicTransportParameters is { } parameters)
        {
            extensions.Add(TlsExtensions.QuicTransportParameters(parameters));
        }

        return extensions;
    }

    private void AppendTo(List<byte> level, byte[] message)
    {
        transcript!.Append(message);
        level.AddRange(message);
    }

    private static void ExpectType(byte[] message, TlsHandshakeType type, string what)
    {
        if (message[0] != (byte)type)
        {
            throw new TlsAlertException(
                TlsAlert.UnexpectedMessage, $"Handshake message type {message[0]} is not the client's {what}.");
        }
    }

    private void ReceiveClientCertificate(byte[] message)
    {
        ExpectType(message, TlsHandshakeType.Certificate, "Certificate");
        var certificate = CertificateMessage.ReadBody(message.AsSpan(HandshakeHeaderLength));
        if (!certificate.CertificateRequestContext.IsEmpty)
        {
            throw new TlsAlertException(
                TlsAlert.IllegalParameter,
                "The client's Certificate does not echo the CertificateRequest's empty certificate_request_context (RFC 8446 section 4.4.2).");
        }

        if (certificate.Entries.Count == 0)
        {
            throw new TlsAlertException(
                TlsAlert.CertificateRequired, "The client sent no certificate, and one is required (RFC 8446 section 4.4.2.4).");
        }

        ReadOnlyMemory<byte>[] chain = [.. certificate.Entries.Select(entry => entry.CertificateData)];
        clientCertificate = LoadClientCertificate(chain[0]);
        if (!options.TrustsClientCertificateChain!(chain))
        {
            throw new TlsAlertException(TlsAlert.BadCertificate, "The client's certificate chain is not trusted.");
        }

        receivedClientCertificateChain = chain;
        transcript!.Append(message);
        state = State.AwaitingClientCertificateVerify;
    }

    private static X509Certificate2 LoadClientCertificate(ReadOnlyMemory<byte> certificateData)
    {
        try
        {
            return X509CertificateLoader.LoadCertificate(certificateData.Span);
        }
        catch (CryptographicException exception)
        {
            throw new TlsAlertException(
                TlsAlert.BadCertificate, $"The client's end-entity certificate is not an X.509 certificate: {exception.Message}");
        }
    }

    private void ReceiveClientCertificateVerify(byte[] message)
    {
        ExpectType(message, TlsHandshakeType.CertificateVerify, "CertificateVerify");
        var verify = CertificateVerify.ReadBody(message.AsSpan(HandshakeHeaderLength));
        if (!TlsCertificateVerifier.ClientSchemes.Contains(verify.Algorithm))
        {
            throw new TlsAlertException(
                TlsAlert.IllegalParameter,
                $"The client's CertificateVerify uses scheme 0x{(ushort)verify.Algorithm:X4}, which the CertificateRequest did not offer (RFC 8446 section 4.4.3).");
        }

        if (!TlsCertificateVerifier.Verifies(clientCertificate!, verify, transcript!.CurrentHash()))
        {
            throw new TlsAlertException(
                TlsAlert.DecryptError, "The client's CertificateVerify does not verify with its certificate's public key (RFC 8446 section 4.4.3).");
        }

        transcript.Append(message);
        ClientCertificateChain = receivedClientCertificateChain;
        state = State.AwaitingClientFinished;
    }

    private void ReceiveClientFinished(byte[] message)
    {
        ExpectType(message, TlsHandshakeType.Finished, "Finished");
        var expectedClientVerifyData = keySchedule!.ComputeFinishedVerifyData(clientHandshakeTrafficSecret, transcript!.CurrentHash());
        if (!CryptographicOperations.FixedTimeEquals(message.AsSpan(HandshakeHeaderLength), expectedClientVerifyData))
        {
            throw new TlsAlertException(TlsAlert.DecryptError, "The client's Finished does not verify (RFC 8446 section 4.4.4).");
        }

        transcript.Append(message);
        state = State.Complete;
    }

    private sealed class Flight
    {
        public List<byte> Initial { get; } = [];

        public List<byte> Handshake { get; } = [];

        public TlsTrafficSecrets? HandshakeSecrets { get; set; }

        public TlsTrafficSecrets? ApplicationSecrets { get; set; }
    }
}
