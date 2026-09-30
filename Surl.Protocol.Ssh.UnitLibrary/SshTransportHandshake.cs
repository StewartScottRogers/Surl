using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The server's side of an SSH connection's first key exchange: the identification lines,
/// the two <c>KEXINIT</c> messages and the negotiation between them, the key exchange method,
/// and <c>NEWKEYS</c> both ways (RFC 4253 sections 4.2, 7 and 8; ADR-0051 decisions 1, 2 and 2.1).
/// </summary>
/// <remarks>
/// Before the client's <c>KEXINIT</c>, and during the key exchange unless it is strict, the
/// client's <c>IGNORE</c>, <c>DEBUG</c> and <c>UNIMPLEMENTED</c> are skipped. Under strict key
/// exchange the client's <c>KEXINIT</c> must be its first packet, nothing but the method's
/// messages and <c>NEWKEYS</c> may follow it, a packet that would wrap the client's sequence
/// number is refused, and each direction's sequence number is set back to 0 after its
/// <c>NEWKEYS</c>. Any other message is <c>DISCONNECT</c> 2. A client's <c>DISCONNECT</c> is
/// noted and ends the exchange without a reply.
/// </remarks>
/// <param name="connection">The connection being served.</param>
/// <param name="context">What the server is told about this exchange.</param>
/// <param name="offer">The algorithms the server offers.</param>
/// <param name="hostKeys">The host keys, one of which signs the exchange hash.</param>
/// <param name="randomSource">Where the cookie, the padding and a finite-field private exponent come from.</param>
internal sealed class SshTransportHandshake(
    IConnection connection,
    ExchangeContext context,
    SshAlgorithmOffer offer,
    SshHostKeySet hostKeys,
    ISshRandomSource randomSource) : ISshKeyExchangeChannel
{
    private readonly SshConnectionReader connectionReader = new(connection);
    private readonly SshPacketWriter packetWriter = new(connection, randomSource);
    private SshPacketReader? packetReader;
    private bool strict;

    /// <summary>
    /// The reader of the client's packets, which reads on after the key exchange; set once the
    /// identification lines are exchanged.
    /// </summary>
    public SshPacketReader? PacketReader => packetReader;

    /// <summary>
    /// The writer of the server's packets, which writes on after the key exchange.
    /// </summary>
    public SshPacketWriter PacketWriter => packetWriter;

    /// <summary>
    /// Runs the connection's first key exchange up to and including both <c>NEWKEYS</c>.
    /// </summary>
    /// <param name="cancellationToken">Cuts the exchange off: the head timeout or the exchange's end.</param>
    /// <returns>The algorithms agreed, the session identifier and the session keys' derivation.</returns>
    /// <exception cref="SshExchangeEndedException">The client closed the connection or sent a <c>DISCONNECT</c>.</exception>
    /// <exception cref="SshDisconnectRequiredException">
    /// The server ends the exchange with a <c>DISCONNECT</c>; a key exchange method not built
    /// yet is <c>DISCONNECT</c> 11 at its first message.
    /// </exception>
    public async Task<SshKeyExchangeResult> RunAsync(CancellationToken cancellationToken)
    {
        await connection.WriteAsync(SshIdentificationLine.ServerLine, cancellationToken);
        var clientLine = await SshIdentificationLine.ReadClientLineAsync(connectionReader, cancellationToken);
        context.Log.Note($"SSH client identification: {SshLogText.Render(clientLine)}");

        packetReader = new SshPacketReader(connectionReader, context.Limits.MaxMessageBytes);
        var serverKexInit = SshKexInit.ForServer(offer, randomSource).ToPayload();
        await packetWriter.WriteAsync(serverKexInit, cancellationToken);

        var clientKexInit = await ReadClientKexInitAsync(cancellationToken);
        var algorithms = SshAlgorithmNegotiator.Negotiate(SshKexInit.Parse(clientKexInit), SshKexInit.Parse(serverKexInit));
        context.Log.Note(algorithms.ToNote());
        strict = algorithms.StrictKeyExchange;
        packetReader.RefusesSequenceWrap = strict;
        if (algorithms.ClientGuessIsWrong)
        {
            await ReadDuringKeyExchangeAsync(IsKeyExchangeMethodMessage, cancellationToken);
        }

        var hostKey = hostKeys.ForSignatureAlgorithm(algorithms.ServerHostKey);
        var hashInput = new SshExchangeHashInput(
            clientLine,
            SshIdentificationLine.ServerLine.Span[..^2],
            clientKexInit,
            serverKexInit,
            hostKey.PublicKeyBlob.Span);

        return await ExchangeKeysAsync(algorithms, hostKey, hashInput, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<SshWireReader> ReadAsync(byte messageNumber, CancellationToken cancellationToken)
    {
        var payload = await ReadDuringKeyExchangeAsync(number => number == messageNumber, cancellationToken);
        var reader = new SshWireReader(payload);
        reader.ReadByte();

        return reader;
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(byte[] payload, CancellationToken cancellationToken) => packetWriter.WriteAsync(payload, cancellationToken);

    private static bool IsSkippable(byte messageNumber) =>
        messageNumber is SshMessageNumber.Ignore or SshMessageNumber.Debug or SshMessageNumber.Unimplemented;

    // A field rather than a method group, so each call site passes one delegate made once.
    private static readonly Func<byte, bool> IsKeyExchangeMethodMessage = messageNumber =>
        messageNumber is >= SshMessageNumber.FirstKeyExchangeMethodMessage and <= SshMessageNumber.LastKeyExchangeMethodMessage;

    private async Task<SshKeyExchangeResult> ExchangeKeysAsync(
        SshNegotiatedAlgorithms algorithms,
        SshHostKey hostKey,
        SshExchangeHashInput hashInput,
        CancellationToken cancellationToken)
    {
        var method = SshKeyExchangeMethod.ForName(algorithms.KeyExchange, randomSource);
        if (method is null)
        {
            await ReadDuringKeyExchangeAsync(IsKeyExchangeMethodMessage, cancellationToken);

            throw new SshDisconnectRequiredException(
                SshDisconnectReason.ByApplication,
                "Key exchange not implemented",
                $"The SSH key exchange {algorithms.KeyExchange} is not built yet; the connection was ended after the negotiation.");
        }

        var output = await method.RunAsync(this, hashInput, hostKey, algorithms.ServerHostKey, cancellationToken);

        await packetWriter.WriteAsync(new[] { SshMessageNumber.NewKeys }, cancellationToken);
        await ReadAsync(SshMessageNumber.NewKeys, cancellationToken);
        if (strict)
        {
            packetWriter.SequenceNumber = 0;
            packetReader!.SequenceNumber = 0;
        }

        packetReader!.RefusesSequenceWrap = false;

        return new SshKeyExchangeResult(
            algorithms,
            output.ExchangeHash,
            new SshKeyDerivation(method.HashAlgorithm, output.SharedSecret, output.ExchangeHash, output.ExchangeHash));
    }

    private async ValueTask<byte[]> ReadClientKexInitAsync(CancellationToken cancellationToken)
    {
        var packetsBefore = 0;
        while (true)
        {
            var payload = await ReadMessageAsync(cancellationToken);
            if (payload[0] == SshMessageNumber.KeyExchangeInit)
            {
                RefuseLateStrictKexInit(payload, packetsBefore);

                return payload;
            }

            if (!IsSkippable(payload[0]))
            {
                throw SshDisconnectRequiredException.ProtocolError($"The client sent SSH message {payload[0]} before its KEXINIT.");
            }

            packetsBefore++;
        }
    }

    private void RefuseLateStrictKexInit(byte[] kexInitPayload, int packetsBefore)
    {
        if (packetsBefore > 0 && SshAlgorithmNegotiator.IsStrict(SshKexInit.Parse(kexInitPayload).KeyExchange, offer.KeyExchange))
        {
            throw SshDisconnectRequiredException.ProtocolError(
                "Under strict key exchange the client's KEXINIT was not its first SSH packet.");
        }
    }

    // Reads up to the next message the key exchange wants (RFC 4253 section 7: nothing else
    // may come between KEXINIT and NEWKEYS but the generic transport messages).
    private async ValueTask<byte[]> ReadDuringKeyExchangeAsync(Func<byte, bool> isWanted, CancellationToken cancellationToken)
    {
        while (true)
        {
            var payload = await ReadMessageAsync(cancellationToken);
            if (isWanted(payload[0]))
            {
                return payload;
            }

            if (strict || !IsSkippable(payload[0]))
            {
                throw SshDisconnectRequiredException.ProtocolError($"The client sent SSH message {payload[0]} during the key exchange.");
            }
        }
    }

    private async ValueTask<byte[]> ReadMessageAsync(CancellationToken cancellationToken)
    {
        var payload = await packetReader!.ReadPayloadAsync(cancellationToken);
        if (payload[0] != SshMessageNumber.Disconnect)
        {
            return payload;
        }

        var reader = new SshWireReader(payload);
        reader.ReadByte();
        var reasonCode = reader.ReadUInt32();
        var description = reader.ReadString();
        context.Log.Note($"SSH disconnect received: {reasonCode} {SshLogText.Render(description.Span)}");

        throw new SshExchangeEndedException(null);
    }
}
