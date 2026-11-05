using System.Security.Cryptography;
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
    private bool finalFlightAcknowledged;
    private bool established;

    private enum HandshakeState
    {
        NotStarted,
        AwaitingClientKeyExchange,
        AwaitingChangeCipherSpec,
        AwaitingFinished,
        ClientFinished,
        Established,
        Closed,
    }

    /// <summary>The suite negotiated, once the server's first flight is built.</summary>
    public DtlsCipherSuite? CipherSuite => negotiation?.Suite;

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
    /// <returns>The record's plaintext, or <see langword="null"/> once the client has closed or failed the association.</returns>
    /// <exception cref="InvalidOperationException">The handshake has not completed.</exception>
    public async ValueTask<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotEstablished();
        while (applicationData.Count == 0 && state == HandshakeState.Established)
        {
            var datagram = await flow.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (AcceptEstablishedDatagram(datagram))
            {
                await records.SendFlightAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return applicationData.TryDequeue(out var plaintext) ? plaintext : null;
    }

    /// <summary>Sends <paramref name="plaintext"/> as one application data record in epoch 1.</summary>
    /// <param name="plaintext">At most 2^14 bytes.</param>
    /// <param name="cancellationToken">Cuts the send off.</param>
    /// <returns>A task that completes once the datagram is sent.</returns>
    /// <exception cref="InvalidOperationException">The handshake has not completed.</exception>
    public ValueTask SendAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken)
    {
        ThrowIfNotEstablished();
        return records.SendAsync(DtlsContentType.ApplicationData, plaintext.ToArray(), cancellationToken);
    }

    /// <summary>Disposes the ephemeral key and the record protections. The flow stays the caller's.</summary>
    public void Dispose()
    {
        ephemeralKey?.Dispose();
        clientProtection?.Dispose();
        serverProtection?.Dispose();
        CryptographicOperations.ZeroMemory(masterSecret);
    }

    private static bool IsClosingAlert(DtlsRecord record) =>
        DtlsAlert.TryRead(record.Plaintext, out var alert)
        && (alert.IsFatal || alert.Description == DtlsAlertDescription.CloseNotify);

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
        negotiation = DtlsServerNegotiation.Negotiate(opening.Hello, options.Certificate, options.AesCcmSupported);
        clientRandom = opening.Hello.Random;
        serverRandom = RandomNumberGenerator.GetBytes(DtlsClientHello.RandomLength);
        ephemeralKey = DtlsEphemeralKey.Create(negotiation.Group);
        var unsigned = new DtlsEcdheServerKeyExchange(
            (ushort)negotiation.Group, ephemeralKey.PublicPoint, (ushort)negotiation.SignatureScheme, []);
        var signature = options.Certificate.Sign(negotiation.SignatureScheme, unsigned.SignedContent(clientRandom, serverRandom));

        nextServerMessageSeq = opening.Message.MessageSeq;
        records.StartFlight();
        var serverHello = new DtlsServerHello(
            DtlsRecordHeader.Dtls12Version, serverRandom, [], negotiation.Suite.Code, 0, negotiation.ServerExtensions);
        AddServerMessage(DtlsHandshakeType.ServerHello, serverHello.Write());
        AddServerMessage(DtlsHandshakeType.Certificate, new DtlsCertificate(options.Certificate.Chain).Write());
        AddServerMessage(DtlsHandshakeType.ServerKeyExchange, (unsigned with { Signature = signature }).Write());
        AddServerMessage(DtlsHandshakeType.ServerHelloDone, DtlsServerHelloDone.Write());
        reassembler = new DtlsHandshakeReassembler(options.MaximumMessageLength, (ushort)(opening.Message.MessageSeq + 1));
        await records.SendFlightAsync(cancellationToken).ConfigureAwait(false);
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
        if (state == HandshakeState.AwaitingClientKeyExchange && message.MessageType == DtlsHandshakeType.ClientKeyExchange)
        {
            AcceptClientKeyExchange(message);
        }
        else if (state == HandshakeState.AwaitingFinished && message.MessageType == DtlsHandshakeType.Finished)
        {
            AcceptFinished(message);
        }
        else
        {
            throw new DtlsAlertException(
                DtlsAlertDescription.UnexpectedMessage, $"{message.MessageType} is not expected while {state}.");
        }
    }

    private void AcceptClientKeyExchange(DtlsHandshakeMessage message)
    {
        var exchange = DtlsEcdheClientKeyExchange.Read(message.Body);
        var preMasterSecret = ephemeralKey!.DeriveSharedSecret(exchange.PublicPoint);
        AddToTranscript(message);
        var suite = negotiation!.Suite;
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
        state = HandshakeState.AwaitingChangeCipherSpec;
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
            if (record.Header.ContentType == DtlsContentType.Alert && IsClosingAlert(record))
            {
                state = HandshakeState.Closed;
                return false;
            }

            if (record.Header.ContentType == DtlsContentType.ApplicationData && record.Header.Epoch == 1)
            {
                finalFlightAcknowledged = true;
                applicationData.Enqueue(record.Plaintext);
            }
            else
            {
                retransmitted |= record.Header.ContentType is DtlsContentType.Handshake or DtlsContentType.ChangeCipherSpec;
            }
        }

        return retransmitted && !finalFlightAcknowledged;
    }

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
