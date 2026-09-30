using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The server's side of an SSH connection's key exchanges: the identification lines, the two
/// <c>KEXINIT</c> messages and the negotiation between them, the key exchange method, and
/// <c>NEWKEYS</c> both ways, after which each direction's packets are protected with the keys
/// just derived (RFC 4253 sections 4.2, 6, 7, 8 and 9; ADR-0051 decisions 1, 2 and 2.1). The
/// first exchange opens the connection; each later one re-keys it, started by the client's
/// <c>KEXINIT</c> or by the server once <see cref="ReExchangeIsDue"/>.
/// </summary>
/// <remarks>
/// Before the client's <c>KEXINIT</c>, and during a key exchange unless it is the first one of a
/// strict connection, the client's <c>IGNORE</c>, <c>DEBUG</c> and <c>UNIMPLEMENTED</c> are
/// skipped. Under strict key exchange the client's first <c>KEXINIT</c> must be its first packet,
/// nothing but the method's messages and <c>NEWKEYS</c> may follow it, a packet that would wrap the
/// client's sequence number is refused, and each direction's sequence number is set back to 0
/// after every <c>NEWKEYS</c>. Any other message is <c>DISCONNECT</c> 2. A client's
/// <c>DISCONNECT</c> is noted and ends the exchange without a reply. A cipher or MAC that is not
/// built yet is <c>DISCONNECT</c> 11 once both <c>NEWKEYS</c> are exchanged, sent under the keys
/// that were in force before them.
/// </remarks>
/// <param name="connection">The connection being served.</param>
/// <param name="context">What the server is told about this exchange.</param>
/// <param name="offer">The algorithms the server offers.</param>
/// <param name="hostKeys">The host keys, one of which signs the exchange hash.</param>
/// <param name="randomSource">Where the cookie, the padding and a finite-field private exponent come from.</param>
/// <param name="reExchangeLimits">When the server re-keys itself; <see cref="SshReExchangeLimits.Default"/> when <see langword="null"/>.</param>
internal sealed class SshTransportHandshake(
    IConnection connection,
    ExchangeContext context,
    SshAlgorithmOffer offer,
    SshHostKeySet hostKeys,
    ISshRandomSource randomSource,
    SshReExchangeLimits? reExchangeLimits = null) : ISshKeyExchangeChannel
{
    // Fields rather than method groups, so each call site passes one delegate made once.
    private static readonly Func<byte, bool> IsKeyExchangeMethodMessage = messageNumber =>
        messageNumber is >= SshMessageNumber.FirstKeyExchangeMethodMessage and <= SshMessageNumber.LastKeyExchangeMethodMessage;

    private static readonly Func<byte, bool> IsKeyExchangeInit = messageNumber => messageNumber == SshMessageNumber.KeyExchangeInit;

    private readonly SshConnectionReader connectionReader = new(connection);
    private readonly SshPacketWriter packetWriter = new(connection, randomSource);
    private readonly SshReExchangeLimits limits = reExchangeLimits ?? SshReExchangeLimits.Default;
    private SshPacketReader? packetReader;
    private byte[] clientLine = [];
    private byte[]? sessionIdentifier;
    private bool strict;
    private long lastExchangeTimestamp;

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
    /// Whether the server should start a key re-exchange: either direction has carried the
    /// limit's bytes under the current keys, or the limit's interval has passed since they were
    /// agreed. Only asked once the first exchange is done.
    /// </summary>
    public bool ReExchangeIsDue =>
        packetReader!.BytesSinceNewKeys >= limits.Bytes
        || packetWriter.BytesSinceNewKeys >= limits.Bytes
        || context.TimeProvider.GetElapsedTime(lastExchangeTimestamp) >= limits.Interval;

    // Whether the key exchange under way is held to strict ordering: the first one, of a strict connection.
    private bool IsStrictlyOrdered => strict && sessionIdentifier is null;

    /// <summary>
    /// Runs the connection's first key exchange up to and including both <c>NEWKEYS</c>, and
    /// protects both directions with its keys.
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
        clientLine = await SshIdentificationLine.ReadClientLineAsync(connectionReader, cancellationToken);
        context.Log.Note($"SSH client identification: {SshLogText.Render(clientLine)}");

        packetReader = new SshPacketReader(connectionReader, context.Limits.MaxMessageBytes);
        var serverKexInit = await WriteServerKexInitAsync(cancellationToken);
        var clientKexInit = await ReadClientKexInitAsync(cancellationToken);

        return await ExchangeAsync(clientKexInit, serverKexInit, cancellationToken);
    }

    /// <summary>
    /// Runs a key re-exchange (RFC 4253, section 9): the server's <c>KEXINIT</c>, the client's if
    /// it has not come yet, the method's messages and <c>NEWKEYS</c> both ways, all under the
    /// keys in force, after which each direction is protected with the new keys. The session
    /// identifier stays the first exchange's.
    /// </summary>
    /// <param name="clientKexInit">
    /// The client's <c>KEXINIT</c> when the client started the re-exchange; <see langword="null"/>
    /// when the server starts it, and reads the client's after sending its own.
    /// </param>
    /// <param name="cancellationToken">Cuts the exchange off.</param>
    /// <returns>The algorithms agreed and the new keys' derivation.</returns>
    /// <exception cref="SshExchangeEndedException">The client closed the connection or sent a <c>DISCONNECT</c>.</exception>
    /// <exception cref="SshDisconnectRequiredException">The server ends the exchange with a <c>DISCONNECT</c>.</exception>
    public async Task<SshKeyExchangeResult> ReExchangeAsync(byte[]? clientKexInit, CancellationToken cancellationToken)
    {
        context.Log.Note($"SSH key re-exchange started by the {(clientKexInit is null ? "server" : "client")}");
        var serverKexInit = await WriteServerKexInitAsync(cancellationToken);
        clientKexInit ??= await ReadDuringKeyExchangeAsync(IsKeyExchangeInit, cancellationToken);

        return await ExchangeAsync(clientKexInit, serverKexInit, cancellationToken);
    }

    /// <summary>
    /// Reads the client's next message, whatever it is; a <c>DISCONNECT</c> is noted and ends
    /// the exchange.
    /// </summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The payload, message number first.</returns>
    /// <exception cref="SshExchangeEndedException">The client closed the connection or sent a <c>DISCONNECT</c>.</exception>
    /// <exception cref="SshDisconnectRequiredException">The packet is refused.</exception>
    public async ValueTask<byte[]> ReadMessageAsync(CancellationToken cancellationToken)
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

    private async ValueTask<byte[]> WriteServerKexInitAsync(CancellationToken cancellationToken)
    {
        var serverKexInit = SshKexInit.ForServer(offer, randomSource).ToPayload();
        await packetWriter.WriteAsync(serverKexInit, cancellationToken);

        return serverKexInit;
    }

    private async Task<SshKeyExchangeResult> ExchangeAsync(byte[] clientKexInit, byte[] serverKexInit, CancellationToken cancellationToken)
    {
        var algorithms = SshAlgorithmNegotiator.Negotiate(SshKexInit.Parse(clientKexInit), SshKexInit.Parse(serverKexInit));
        if (sessionIdentifier is null)
        {
            context.Log.Note(algorithms.ToNote());
            strict = algorithms.StrictKeyExchange;
            packetReader!.RefusesSequenceWrap = strict;
        }

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
        var identifier = sessionIdentifier ?? output.ExchangeHash;
        var keys = new SshKeyDerivation(method.HashAlgorithm, output.SharedSecret, output.ExchangeHash, identifier);

        await packetWriter.WriteAsync(new[] { SshMessageNumber.NewKeys }, cancellationToken);
        await ReadAsync(SshMessageNumber.NewKeys, cancellationToken);
        if (strict)
        {
            packetWriter.SequenceNumber = 0;
            packetReader!.SequenceNumber = 0;
        }

        packetReader!.RefusesSequenceWrap = false;
        sessionIdentifier = identifier;
        UseNewKeys(algorithms, keys);

        return new SshKeyExchangeResult(algorithms, identifier, keys);
    }

    private void UseNewKeys(SshNegotiatedAlgorithms algorithms, SshKeyDerivation keys)
    {
        var inbound = SshPacketProtection.Create(algorithms.CipherClientToServer, algorithms.MacClientToServer, keys, clientToServer: true);
        var outbound = SshPacketProtection.Create(algorithms.CipherServerToClient, algorithms.MacServerToClient, keys, clientToServer: false);
        if (inbound is null || outbound is null)
        {
            throw new SshDisconnectRequiredException(
                SshDisconnectReason.ByApplication,
                "Packet protection not implemented",
                $"The SSH packet protection {algorithms.CipherClientToServer}/{algorithms.CipherServerToClient} "
                + "is not built yet; the connection was ended after NEWKEYS.");
        }

        packetReader!.UseProtection(inbound);
        packetWriter.UseProtection(outbound);
        lastExchangeTimestamp = context.TimeProvider.GetTimestamp();
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

            if (IsStrictlyOrdered || !IsSkippable(payload[0]))
            {
                throw SshDisconnectRequiredException.ProtocolError($"The client sent SSH message {payload[0]} during the key exchange.");
            }
        }
    }
}
