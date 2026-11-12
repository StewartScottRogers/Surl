using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Surl.Protocol.Abstractions;

namespace Surl.Dtls;

/// <summary>
/// The server side of one DTLS 1.2 association over its <see cref="IDatagramFlow"/> (ADR-0087):
/// the stateless cookie exchange, then a full ECDHE handshake under the server's certificate,
/// with the server's flight retransmitted on the <see cref="DtlsServerOptions.TimeProvider"/>'s
/// timer, then application data in epoch 1.
/// </summary>
/// <remarks>
/// The flights (RFC 6347 section 4.2.4): the client's hello, answered with
/// <c>HelloVerifyRequest</c> while its cookie is missing or wrong; the hello with the cookie,
/// answered with <c>ServerHello</c>, <c>Certificate</c>, <c>ServerKeyExchange</c> and
/// <c>ServerHelloDone</c>; the client's <c>ClientKeyExchange</c>, <c>ChangeCipherSpec</c> and
/// <c>Finished</c>, answered with <c>ChangeCipherSpec</c> and <c>Finished</c>.
/// </remarks>
/// <param name="flow">The association's datagram flow; it stays the caller's to dispose.</param>
/// <param name="options">The certificate, cookie secrets, clock and limits.</param>
public sealed class DtlsServerSession(IDatagramFlow flow, DtlsServerOptions options) : IDisposable
{
    private const int UnknownIdentityKeyLength = 32;

    /// <summary>The client's handshake message each waiting state takes, and what takes it.</summary>
    private static readonly Dictionary<HandshakeState, (DtlsHandshakeType Type, Action<DtlsServerSession, DtlsHandshakeMessage> Accept)> ExpectedMessages = new()
    {
        [HandshakeState.AwaitingClientCertificate] = (DtlsHandshakeType.Certificate, (session, message) => session.AcceptClientCertificate(message)),
        [HandshakeState.AwaitingClientKeyExchange] = (DtlsHandshakeType.ClientKeyExchange, (session, message) => session.AcceptClientKeyExchange(message)),
        [HandshakeState.AwaitingCertificateVerify] = (DtlsHandshakeType.CertificateVerify, (session, message) => session.AcceptCertificateVerify(message)),
        [HandshakeState.AwaitingFinished] = (DtlsHandshakeType.Finished, (session, message) => session.AcceptFinished(message)),
    };

    private readonly DtlsAssociationRecords records = new(flow);
    private readonly List<byte> transcript = [];
    private readonly Queue<byte[]> applicationData = new();
    private HandshakeState state = HandshakeState.NotStarted;
    private DtlsServerNegotiation? negotiation;
    private DtlsEphemeralKey? ephemeralKey;
    private DtlsHandshakeReassembler? reassembler;
    private DtlsRecordProtection? clientProtection;
    private DtlsRecordProtection? serverProtection;
    private byte[] clientRandom = [];
    private byte[] serverRandom = [];
    private byte[] masterSecret = [];
    private ushort nextServerMessageSeq;
    private DtlsClientCertificate? clientCertificate;
    private byte[]? clientPreSharedKeyIdentity;
    private bool finalFlightAcknowledged;
    private bool closeNotifyOwed;
    private int renegotiationRefusalsOwed;
    private bool established;

    private enum HandshakeState
    {
        NotStarted,
        AwaitingClientCertificate,
        AwaitingClientKeyExchange,
        AwaitingCertificateVerify,
        AwaitingChangeCipherSpec,
        AwaitingFinished,
        ClientFinished,
        Established,
        Closed,
    }

    /// <summary>The suite negotiated, once the server's first flight is built.</summary>
    public DtlsCipherSuite? CipherSuite => negotiation?.Suite;

    /// <summary>
    /// The most plaintext <see cref="SendAsync"/> carries in one record, which travels as one datagram:
    /// what fits a <see cref="DtlsHandshakeFragmenter.MaximumDatagramLength"/>-byte datagram after the
    /// 13-byte record header and the negotiated suite's explicit nonce and tag (ADR-0087 decision 7),
    /// or the client's <c>max_fragment_length</c> when that is smaller (RFC 6066 section 4). Under
    /// AES-GCM and AES-CCM that is 1163 bytes, under AES-CCM_8 1171.
    /// </summary>
    /// <exception cref="InvalidOperationException">The handshake has not completed.</exception>
    public int MaximumApplicationDataLength
    {
        get
        {
            ThrowIfNotEstablished();
            var datagramRoom = DtlsHandshakeFragmenter.MaximumDatagramLength - DtlsRecordHeader.Size - serverProtection!.Overhead;
            return Math.Min(datagramRoom, records.MaximumPlaintextLength);
        }
    }

    /// <summary>
    /// The PSK identity the client authenticated with, as UTF-8 text (RFC 4279 section 5.1), once a
    /// PSK handshake has completed; <see langword="null"/> before then and after a certificate
    /// handshake. A completed PSK handshake proves the client holds that identity's key, since a wrong
    /// or unknown one ends it with <c>decrypt_error</c>. Bytes that are not UTF-8 read as U+FFFD.
    /// </summary>
    public string? ClientPreSharedKeyIdentity =>
        established && clientPreSharedKeyIdentity is not null ? Encoding.UTF8.GetString(clientPreSharedKeyIdentity) : null;

    /// <summary>
    /// The certificate the client presented, once a certificate handshake under
    /// <see cref="DtlsServerOptions.ClientCertificateAuthorities"/> has completed; <see langword="null"/>
    /// before then, after a handshake that asked for none, and after a PSK handshake. It stays the
    /// session's and is disposed with it.
    /// </summary>
    public X509Certificate2? ClientCertificate => established ? clientCertificate?.Leaf : null;

    /// <summary>
    /// Whether <see cref="ClientCertificate"/> chained to a <see cref="DtlsServerOptions.ClientCertificateAuthorities"/>
    /// anchor, was valid at the <see cref="DtlsServerOptions.TimeProvider"/>'s time and signed the client's
    /// <c>CertificateVerify</c>. A certificate failing any of those ends the handshake with its alert, so
    /// this is true exactly when <see cref="ClientCertificate"/> is reported.
    /// </summary>
    public bool ClientCertificateVerified => ClientCertificate is not null;

    /// <summary>
    /// Runs the handshake: waits, keeping no state, for a hello with a valid cookie, then completes
    /// the handshake within <see cref="DtlsServerOptions.HandshakeTimeout"/>.
    /// </summary>
    /// <param name="cancellationToken">Ends the exchange, such as at the idle timeout.</param>
    /// <returns>How the handshake ended.</returns>
    /// <exception cref="InvalidOperationException">The handshake has already run.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<DtlsHandshakeOutcome> HandshakeAsync(CancellationToken cancellationToken)
    {
        if (state != HandshakeState.NotStarted)
        {
            throw new InvalidOperationException("A DTLS session handshakes once.");
        }

        var opening = await AwaitVerifiedClientHelloAsync(cancellationToken).ConfigureAwait(false);
        using var limit = new CancellationTokenSource(options.HandshakeTimeout, options.TimeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
        try
        {
            await SendServerFlightAsync(opening, linked.Token).ConfigureAwait(false);
            await AwaitClientFinalFlightAsync(linked.Token).ConfigureAwait(false);
            await SendFinalFlightAsync(cancellationToken).ConfigureAwait(false);
            state = HandshakeState.Established;
            established = true;
            return new DtlsHandshakeOutcome(DtlsHandshakeResult.Completed, null);
        }
        catch (DtlsAlertException alert)
        {
            return await FailAsync(alert.Description, cancellationToken).ConfigureAwait(false);
        }
        catch (DtlsDecodeErrorException)
        {
            return await FailAsync(DtlsAlertDescription.DecodeError, cancellationToken).ConfigureAwait(false);
        }
        catch (PeerAlertException peer)
        {
            state = HandshakeState.Closed;
            return new DtlsHandshakeOutcome(DtlsHandshakeResult.AlertReceived, peer.Description);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            state = HandshakeState.Closed;
            return new DtlsHandshakeOutcome(DtlsHandshakeResult.TimedOut, null);
        }
    }

    /// <summary>
    /// Waits for the next application data record. The client's final flight arriving again
    /// before its first application data is answered with the server's final flight again.
    /// </summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>
    /// The record's plaintext, or <see langword="null"/> once the association has ended: the client
    /// sent <c>close_notify</c>, which is answered with <c>close_notify</c> (RFC 5246 section 7.2.1),
    /// or a fatal alert, which is not answered; or the server closed it.
    /// </returns>
    /// <exception cref="InvalidOperationException">The handshake has not completed.</exception>
    public async ValueTask<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotEstablished();
        while (applicationData.Count == 0 && state == HandshakeState.Established)
        {
            var datagram = await flow.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            var finalFlightAgain = AcceptEstablishedDatagram(datagram);
            await RefuseRenegotiationsAsync(cancellationToken).ConfigureAwait(false);
            if (finalFlightAgain)
            {
                await records.SendFlightAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (closeNotifyOwed)
        {
            await SendCloseNotifyAsync(cancellationToken).ConfigureAwait(false);
        }

        return applicationData.TryDequeue(out var plaintext) ? plaintext : null;
    }

    /// <summary>Sends <paramref name="plaintext"/> as one application data record in epoch 1.</summary>
    /// <param name="plaintext">At most <see cref="MaximumApplicationDataLength"/> bytes.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the datagram is sent.</returns>
    /// <exception cref="InvalidOperationException">The handshake has not completed, or the association has ended.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="plaintext"/> is longer than <see cref="MaximumApplicationDataLength"/>.</exception>
    public async ValueTask SendAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken)
    {
        ThrowIfNotEstablished();
        if (state == HandshakeState.Closed)
        {
            throw new InvalidOperationException("The DTLS association has ended.");
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(plaintext.Length, MaximumApplicationDataLength, nameof(plaintext));

        if (records.NextRecordIsTheLast)
        {
            // The last sequence number ends the association with close_notify; the data is not sent.
            state = HandshakeState.Closed;
            await SendCloseNotifyAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await records.SendAsync(DtlsContentType.ApplicationData, plaintext.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Restarts the epoch 1 sequence numbers at <paramref name="nextSequenceNumber"/>, for tests.</summary>
    /// <param name="nextSequenceNumber">The next epoch 1 sequence number.</param>
    internal void ContinueEpoch1WritesFrom(long nextSequenceNumber) => records.ContinueEpoch1WritesFrom(nextSequenceNumber);

    /// <summary>
    /// Ends an established association with one warning <c>close_notify</c> in epoch 1 (RFC 5246
    /// section 7.2.1), as at the idle timeout or the maximum duration (ADR-0087 decision 9). It sends
    /// nothing before the handshake completes, nor once the association has ended: a <c>close_notify</c>
    /// already sent or answered, or a fatal alert received.
    /// </summary>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the alert is sent, or at once when none is due.</returns>
    public async ValueTask CloseAsync(CancellationToken cancellationToken)
    {
        if (state == HandshakeState.Established)
        {
            state = HandshakeState.Closed;
            await SendCloseNotifyAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Disposes the ephemeral key and the record protections. The flow stays the caller's.</summary>
    public void Dispose()
    {
        ephemeralKey?.Dispose();
        clientCertificate?.Dispose();
        clientProtection?.Dispose();
        serverProtection?.Dispose();
        CryptographicOperations.ZeroMemory(masterSecret);
    }

    /// <summary>
    /// Whether an alert record ends the association: a fatal alert, or <c>close_notify</c> at any
    /// level. A warning other than <c>close_notify</c> is ignored (ADR-0087 decision 10).
    /// </summary>
    private static bool IsClosingAlert(DtlsRecord record, out bool isCloseNotify)
    {
        isCloseNotify = false;
        if (!DtlsAlert.TryRead(record.Plaintext, out var alert))
        {
            return false;
        }

        isCloseNotify = !alert.IsFatal && alert.Description == DtlsAlertDescription.CloseNotify;
        return alert.IsFatal || isCloseNotify;
    }

    /// <summary>
    /// Answers each epoch 1 <c>ClientHello</c> with a warning <c>no_renegotiation</c>; the
    /// association carries on (ADR-0087 decision 6, RFC 5246 section 7.2.2).
    /// </summary>
    private async ValueTask RefuseRenegotiationsAsync(CancellationToken cancellationToken)
    {
        for (; renegotiationRefusalsOwed > 0; renegotiationRefusalsOwed--)
        {
            await records.SendAsync(
                DtlsContentType.Alert, new DtlsAlert(IsFatal: false, DtlsAlertDescription.NoRenegotiation).Write(), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private ValueTask SendCloseNotifyAsync(CancellationToken cancellationToken)
    {
        closeNotifyOwed = false;
        return records.SendAsync(
            DtlsContentType.Alert, new DtlsAlert(IsFatal: false, DtlsAlertDescription.CloseNotify).Write(), cancellationToken);
    }

    private void ThrowIfNotEstablished()
    {
        if (!established)
        {
            throw new InvalidOperationException("The DTLS handshake has not completed.");
        }
    }

    private async ValueTask<DtlsVerifiedClientHello> AwaitVerifiedClientHelloAsync(CancellationToken cancellationToken)
    {
        var datagram = flow.FirstDatagram;
        while (true)
        {
            var verified = DtlsCookieExchange.Answer(
                datagram.Span, flow.RemoteEndPoint, options.CookieSecrets, out var helloVerifyRequest);
            if (verified is not null)
            {
                return verified;
            }

            if (helloVerifyRequest is not null)
            {
                await flow.SendAsync(helloVerifyRequest, cancellationToken).ConfigureAwait(false);
            }

            datagram = await flow.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask SendServerFlightAsync(DtlsVerifiedClientHello opening, CancellationToken cancellationToken)
    {
        records.ContinueEpoch0WritesFrom(opening.RecordSequenceNumber);
        AddToTranscript(opening.Message);
        state = HandshakeState.AwaitingClientKeyExchange;
        var requestsClientCertificate = options.ClientCertificateAuthorities is not null;
        negotiation = DtlsServerNegotiation.Negotiate(
            opening.Hello, options.Certificate, options.PreSharedKeys is not null, options.AesCcmSupported, requestsClientCertificate);
        records.MaximumPlaintextLength = negotiation.MaximumFragmentLength ?? DtlsRecordWriter.MaximumPlaintextLength;
        clientRandom = opening.Hello.Random;
        serverRandom = RandomNumberGenerator.GetBytes(DtlsClientHello.RandomLength);
        nextServerMessageSeq = opening.Message.MessageSeq;
        records.StartFlight();
        var serverHello = new DtlsServerHello(
            DtlsRecordHeader.Dtls12Version, serverRandom, [], negotiation.Suite.Code, 0, negotiation.ServerExtensions);
        AddServerMessage(DtlsHandshakeType.ServerHello, serverHello.Write());
        if (!negotiation.Suite.UsesPreSharedKey)
        {
            AddCertificateAndServerKeyExchange(options.Certificate!, negotiation.Group!.Value, negotiation.SignatureScheme!.Value);
            AddCertificateRequestWhenRequired(requestsClientCertificate);
        }

        AddServerMessage(DtlsHandshakeType.ServerHelloDone, DtlsServerHelloDone.Write());
        reassembler = new DtlsHandshakeReassembler(options.MaximumMessageLength, (ushort)(opening.Message.MessageSeq + 1));
        await records.SendFlightAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a certificate suite's <c>Certificate</c> and signed ECDHE <c>ServerKeyExchange</c>; a PSK
    /// suite sends neither, as no identity hint is sent (RFC 4279 section 2, ADR-0087 decision 3).
    /// </summary>
    private void AddCertificateAndServerKeyExchange(DtlsServerCertificate certificate, DtlsNamedGroup group, DtlsSignatureScheme scheme)
    {
        ephemeralKey = DtlsEphemeralKey.Create(group);
        var unsigned = new DtlsEcdheServerKeyExchange((ushort)group, ephemeralKey.PublicPoint, (ushort)scheme, []);
        var signature = certificate.Sign(scheme, unsigned.SignedContent(clientRandom, serverRandom));
        var certificateBody = negotiation!.UsesRawPublicKey
            ? new DtlsRawPublicKeyCertificate(certificate.SubjectPublicKeyInfo).Write()
            : new DtlsCertificate(certificate.Chain).Write();
        AddServerMessage(DtlsHandshakeType.Certificate, certificateBody);
        AddServerMessage(DtlsHandshakeType.ServerKeyExchange, (unsigned with { Signature = signature }).Write());
    }

    /// <summary>
    /// Under <c>--cacert</c>, adds <c>CertificateRequest</c> and waits for the client's
    /// <c>Certificate</c> first (ADR-0087 decision 3).
    /// </summary>
    private void AddCertificateRequestWhenRequired(bool requestsClientCertificate)
    {
        if (requestsClientCertificate)
        {
            AddServerMessage(DtlsHandshakeType.CertificateRequest, DtlsClientCertificate.Request.Write());
            state = HandshakeState.AwaitingClientCertificate;
        }
    }

    private async ValueTask AwaitClientFinalFlightAsync(CancellationToken cancellationToken)
    {
        var timeout = DtlsServerOptions.InitialRetransmissionTimeout;
        while (state != HandshakeState.ClientFinished)
        {
            var datagram = await ReceiveBeforeTimeoutAsync(timeout, cancellationToken).ConfigureAwait(false);
            if (datagram is null)
            {
                timeout = TimeSpan.FromTicks(Math.Min(timeout.Ticks * 2, DtlsServerOptions.MaximumRetransmissionTimeout.Ticks));
                await records.SendFlightAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (AcceptHandshakeDatagram(datagram.Value))
            {
                await records.SendFlightAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<ReadOnlyMemory<byte>?> ReceiveBeforeTimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timer = new CancellationTokenSource(timeout, options.TimeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try
        {
            return await flow.ReceiveAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// Takes in one datagram of the client's final flight.
    /// </summary>
    /// <returns>Whether the client sent part of a flight again, so the server's flight is resent.</returns>
    private bool AcceptHandshakeDatagram(ReadOnlyMemory<byte> datagram)
    {
        var retransmitted = false;
        foreach (var record in records.Read(datagram))
        {
            retransmitted |= AcceptHandshakeRecord(record);
            if (state == HandshakeState.ClientFinished)
            {
                return false;
            }
        }

        if (records.Epoch1AuthenticationFailures > 0)
        {
            // The only epoch 1 record a client sends before the server's Finished is its own
            // Finished; failing its tag means the client derived other keys, which is how a wrong
            // or unknown PSK shows (ADR-0087 decision 10, as amended by BL-415).
            throw new DtlsAlertException(DtlsAlertDescription.DecryptError, "The client's Finished record does not authenticate.");
        }

        return retransmitted;
    }

    private bool AcceptHandshakeRecord(DtlsRecord record)
    {
        switch (record.Header.ContentType)
        {
            case DtlsContentType.Alert:
                ThrowIfFatal(record);
                return false;
            case DtlsContentType.ChangeCipherSpec:
                return AcceptChangeCipherSpec(record);
            case DtlsContentType.Handshake:
                return AcceptHandshakeFragments(record);
            default:
                return false;
        }
    }

    private static void ThrowIfFatal(DtlsRecord record)
    {
        if (DtlsAlert.TryRead(record.Plaintext, out var alert) && alert.IsFatal)
        {
            throw new PeerAlertException(alert.Description);
        }
    }

    /// <summary>
    /// A <c>ChangeCipherSpec</c> after <c>ClientKeyExchange</c> starts epoch 1 reads; one before it
    /// is ignored, as reordering may bring it early and the client resends its flight; one after is
    /// the client's flight sent again.
    /// </summary>
    private bool AcceptChangeCipherSpec(DtlsRecord record)
    {
        if (state != HandshakeState.AwaitingChangeCipherSpec)
        {
            return state == HandshakeState.AwaitingFinished;
        }

        if (record.Plaintext is not [1])
        {
            throw new DtlsDecodeErrorException("A ChangeCipherSpec carries the single byte 1.");
        }

        records.StartProtectedReads(clientProtection!);
        state = HandshakeState.AwaitingFinished;
        return false;
    }

    private bool AcceptHandshakeFragments(DtlsRecord record)
    {
        if (state == HandshakeState.AwaitingFinished && record.Header.Epoch == 0)
        {
            return true;
        }

        var stale = false;
        var fragments = record.Plaintext.AsSpan();
        while (!fragments.IsEmpty)
        {
            var outcome = reassembler!.Accept(fragments, out var consumed);
            fragments = fragments[consumed..];
            stale |= outcome == DtlsHandshakeFragmentOutcome.Stale;
            ThrowIfRefused(outcome);
        }

        while (state != HandshakeState.ClientFinished && reassembler!.TryTakeNext(out var message))
        {
            AcceptMessage(message);
        }

        return stale;
    }

    private static void ThrowIfRefused(DtlsHandshakeFragmentOutcome outcome)
    {
        if (outcome is DtlsHandshakeFragmentOutcome.Malformed or DtlsHandshakeFragmentOutcome.Contradictory)
        {
            throw new DtlsDecodeErrorException($"A handshake fragment is {outcome}.");
        }

        if (outcome == DtlsHandshakeFragmentOutcome.OverLimit)
        {
            throw new DtlsAlertException(DtlsAlertDescription.HandshakeFailure, "The handshake messages exceed --max-message.");
        }
    }

    private void AcceptMessage(DtlsHandshakeMessage message)
    {
        if (!ExpectedMessages.TryGetValue(state, out var expected) || expected.Type != message.MessageType)
        {
            throw new DtlsAlertException(
                DtlsAlertDescription.UnexpectedMessage, $"{message.MessageType} is not expected while {state}.");
        }

        expected.Accept(this, message);
    }

    /// <summary>
    /// Takes the client's <c>Certificate</c> under <c>--cacert</c>: it must hold a certificate that
    /// chains to an anchor and is valid now by the <see cref="DtlsServerOptions.TimeProvider"/>.
    /// </summary>
    private void AcceptClientCertificate(DtlsHandshakeMessage message)
    {
        var chain = DtlsCertificate.Read(message.Body).Certificates;
        clientCertificate = DtlsClientCertificate.Validate(
            chain, options.ClientCertificateAuthorities!, options.TimeProvider.GetUtcNow());
        AddToTranscript(message);
        state = HandshakeState.AwaitingClientKeyExchange;
    }

    /// <summary>
    /// Takes the client's <c>CertificateVerify</c>: its signature over every handshake message
    /// before it (RFC 5246 section 7.4.8) must verify under the client certificate's key.
    /// </summary>
    private void AcceptCertificateVerify(DtlsHandshakeMessage message)
    {
        var verify = DtlsCertificateVerify.Read(message.Body);
        if (!clientCertificate!.Verifies(verify, [.. transcript]))
        {
            throw new DtlsAlertException(DtlsAlertDescription.DecryptError, "The client's CertificateVerify does not verify.");
        }

        AddToTranscript(message);
        state = HandshakeState.AwaitingChangeCipherSpec;
    }

    private void AcceptClientKeyExchange(DtlsHandshakeMessage message)
    {
        var suite = negotiation!.Suite;
        var preMasterSecret = suite.UsesPreSharedKey
            ? PreSharedKeyPreMasterSecret(DtlsPskClientKeyExchange.Read(message.Body).Identity)
            : ephemeralKey!.DeriveSharedSecret(DtlsEcdheClientKeyExchange.Read(message.Body).PublicPoint);
        AddToTranscript(message);
        masterSecret = negotiation.UsesExtendedMasterSecret
            ? DtlsPseudorandomFunction.ExtendedMasterSecret(suite.PrfHash, preMasterSecret, TranscriptHash())
            : DtlsPseudorandomFunction.MasterSecret(suite.PrfHash, preMasterSecret, clientRandom, serverRandom);
        CryptographicOperations.ZeroMemory(preMasterSecret);

        var keyBlock = DtlsPseudorandomFunction.KeyBlock(
            suite.PrfHash, masterSecret, clientRandom, serverRandom, suite.KeyBlockLength);
        var keyLength = suite.KeyLength;
        var salts = 2 * keyLength;
        clientProtection = new DtlsRecordProtection(
            suite.Aead, keyBlock.AsSpan(0, keyLength), keyBlock.AsSpan(salts, DtlsRecordProtection.SaltSize));
        serverProtection = new DtlsRecordProtection(
            suite.Aead,
            keyBlock.AsSpan(keyLength, keyLength),
            keyBlock.AsSpan(salts + DtlsRecordProtection.SaltSize, DtlsRecordProtection.SaltSize));
        CryptographicOperations.ZeroMemory(keyBlock);
        state = clientCertificate is null ? HandshakeState.AwaitingChangeCipherSpec : HandshakeState.AwaitingCertificateVerify;
    }

    /// <summary>
    /// The plain PSK premaster secret (RFC 4279 section 2): the key's length N as 2 bytes, N zero
    /// bytes, N again, then the key. An identity the lookup does not know gets a random key instead,
    /// so the handshake carries on and the client's <c>Finished</c> fails with <c>decrypt_error</c>:
    /// never <c>unknown_psk_identity</c>, which would tell a peer which identities exist (ADR-0087
    /// decision 10). The identity is kept for <see cref="ClientPreSharedKeyIdentity"/>, which reports
    /// it only once <c>Finished</c> has proved the key.
    /// </summary>
    private byte[] PreSharedKeyPreMasterSecret(byte[] identity)
    {
        clientPreSharedKeyIdentity = identity;
        var key = options.PreSharedKeys!(identity) is { Length: > 0 and <= ushort.MaxValue } known ? known : RandomNumberGenerator.GetBytes(UnknownIdentityKeyLength);
        var preMasterSecret = new byte[4 + (2 * key.Length)];
        var length = (ushort)key.Length;
        BinaryPrimitives.WriteUInt16BigEndian(preMasterSecret, length);
        BinaryPrimitives.WriteUInt16BigEndian(preMasterSecret.AsSpan(2 + key.Length), length);
        key.CopyTo(preMasterSecret, 4 + key.Length);
        return preMasterSecret;
    }

    private void AcceptFinished(DtlsHandshakeMessage message)
    {
        var finished = DtlsFinished.Read(message.Body);
        var expected = DtlsPseudorandomFunction.ClientFinishedVerifyData(negotiation!.Suite.PrfHash, masterSecret, TranscriptHash());
        if (!CryptographicOperations.FixedTimeEquals(expected, finished.VerifyData))
        {
            throw new DtlsAlertException(DtlsAlertDescription.DecryptError, "The client's Finished does not verify.");
        }

        AddToTranscript(message);
        state = HandshakeState.ClientFinished;
    }

    private async ValueTask SendFinalFlightAsync(CancellationToken cancellationToken)
    {
        var verifyData = DtlsPseudorandomFunction.ServerFinishedVerifyData(negotiation!.Suite.PrfHash, masterSecret, TranscriptHash());
        records.StartFlight();
        records.AddChangeCipherSpecToFlight();
        records.StartProtectedWrites(serverProtection!);
        records.AddToFlight(new DtlsHandshakeMessage(DtlsHandshakeType.Finished, nextServerMessageSeq, new DtlsFinished(verifyData).Write()));
        await records.SendFlightAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DtlsHandshakeOutcome> FailAsync(DtlsAlertDescription description, CancellationToken cancellationToken)
    {
        state = HandshakeState.Closed;
        await records.SendAsync(DtlsContentType.Alert, new DtlsAlert(IsFatal: true, description).Write(), cancellationToken)
            .ConfigureAwait(false);
        return new DtlsHandshakeOutcome(DtlsHandshakeResult.AlertSent, description);
    }

    /// <summary>
    /// Takes in one datagram once established.
    /// </summary>
    /// <returns>Whether the client sent its final flight again before any application data, so the server's is resent.</returns>
    private bool AcceptEstablishedDatagram(ReadOnlyMemory<byte> datagram)
    {
        var retransmitted = false;
        foreach (var record in records.Read(datagram))
        {
            if (record.Header.ContentType == DtlsContentType.Alert && IsClosingAlert(record, out var isCloseNotify))
            {
                state = HandshakeState.Closed;
                closeNotifyOwed = isCloseNotify;
                return false;
            }

            retransmitted |= AcceptEstablishedRecord(record);
        }

        return retransmitted && !finalFlightAcknowledged;
    }

    /// <summary>
    /// Takes in one record other than a closing alert once established: epoch 1 application data
    /// is queued, an epoch 1 <c>ClientHello</c> is owed a warning <c>no_renegotiation</c>
    /// (ADR-0087 decision 6), and any other handshake or <c>ChangeCipherSpec</c> record is the
    /// client's final flight again.
    /// </summary>
    /// <returns>Whether the record is part of the client's final flight sent again.</returns>
    private bool AcceptEstablishedRecord(DtlsRecord record)
    {
        var type = record.Header.ContentType;
        if (record.Header.Epoch == 1 && type == DtlsContentType.ApplicationData)
        {
            finalFlightAcknowledged = true;
            applicationData.Enqueue(record.Plaintext);
            return false;
        }

        if (record.Header.Epoch == 1 && type == DtlsContentType.Handshake && StartsClientHello(record.Plaintext))
        {
            renegotiationRefusalsOwed++;
            return false;
        }

        return type is DtlsContentType.Handshake or DtlsContentType.ChangeCipherSpec;
    }

    private static bool StartsClientHello(byte[] fragments) =>
        DtlsHandshakeHeader.TryRead(fragments, out var header) && header.MessageType == DtlsHandshakeType.ClientHello;

    private void AddServerMessage(DtlsHandshakeType type, byte[] body)
    {
        var message = new DtlsHandshakeMessage(type, nextServerMessageSeq++, body);
        AddToTranscript(message);
        records.AddToFlight(message);
    }

    private void AddToTranscript(DtlsHandshakeMessage message) => transcript.AddRange(message.ToUnfragmentedBytes());

    private byte[] TranscriptHash() => CryptographicOperations.HashData(negotiation!.Suite.PrfHash, [.. transcript]);

    private sealed class PeerAlertException(DtlsAlertDescription description) : Exception($"The client sent fatal alert {description}.")
    {
        public DtlsAlertDescription Description { get; } = description;
    }
}
