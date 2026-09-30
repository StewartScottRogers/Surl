using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The server's side of an SSH connection's opening, up to the key exchange: the
/// identification lines, the two <c>KEXINIT</c> messages and the negotiation between them
/// (RFC 4253 sections 4.2, 7 and 7.1; ADR-0051 decisions 1, 2 and 2.1).
/// </summary>
/// <remarks>
/// Before the client's <c>KEXINIT</c>, and during the key exchange unless it is strict, the
/// client's <c>IGNORE</c>, <c>DEBUG</c> and <c>UNIMPLEMENTED</c> are skipped. Under strict key
/// exchange the client's <c>KEXINIT</c> must be its first packet and nothing but key exchange
/// method messages may follow it. Any other message is <c>DISCONNECT</c> 2. A client's
/// <c>DISCONNECT</c> is noted and ends the exchange without a reply. The key exchange method
/// itself is BL-160's.
/// </remarks>
/// <param name="connection">The connection being served.</param>
/// <param name="context">What the server is told about this exchange.</param>
/// <param name="offer">The algorithms the server offers.</param>
/// <param name="randomSource">Where the cookie and the padding come from.</param>
internal sealed class SshTransportHandshake(
    IConnection connection,
    ExchangeContext context,
    SshAlgorithmOffer offer,
    ISshRandomSource randomSource)
{
    private readonly SshConnectionReader connectionReader = new(connection);
    private readonly SshPacketWriter packetWriter = new(connection, randomSource);
    private SshPacketReader? packetReader;

    /// <summary>
    /// Runs the opening up to the key exchange method's first message from the client, which
    /// it reads (after a wrongly guessed one, discarded) and does not answer.
    /// </summary>
    /// <param name="cancellationToken">Cuts the opening off: the head timeout or the exchange's end.</param>
    /// <returns>The algorithms agreed.</returns>
    /// <exception cref="SshExchangeEndedException">The client closed the connection or sent a <c>DISCONNECT</c>.</exception>
    /// <exception cref="SshDisconnectRequiredException">The server ends the exchange with a <c>DISCONNECT</c>.</exception>
    public async Task<SshNegotiatedAlgorithms> RunAsync(CancellationToken cancellationToken)
    {
        await connection.WriteAsync(SshIdentificationLine.ServerLine, cancellationToken);
        var clientLine = await SshIdentificationLine.ReadClientLineAsync(connectionReader, cancellationToken);
        context.Log.Note($"SSH client identification: {SshLogText.Render(clientLine)}");

        packetReader = new SshPacketReader(connectionReader, context.Limits.MaxMessageBytes);
        var serverKexInit = SshKexInit.ForServer(offer, randomSource);
        await packetWriter.WriteAsync(serverKexInit.ToPayload(), cancellationToken);

        var (clientKexInit, packetsBeforeKexInit) = await ReadClientKexInitAsync(cancellationToken);
        var strict = clientKexInit.KeyExchange.Contains(SshAlgorithmOffer.StrictKeyExchangeClientMarker);
        if (strict && packetsBeforeKexInit > 0)
        {
            throw SshDisconnectRequiredException.ProtocolError(
                "Under strict key exchange the client's KEXINIT was not its first SSH packet.");
        }

        var algorithms = SshAlgorithmNegotiator.Negotiate(clientKexInit, serverKexInit);
        context.Log.Note(algorithms.ToNote());
        if (algorithms.ClientGuessIsWrong)
        {
            await ReadKeyExchangeMessageAsync(strict, cancellationToken);
        }

        await ReadKeyExchangeMessageAsync(strict, cancellationToken);

        return algorithms;
    }

    private static bool IsSkippable(byte messageNumber) =>
        messageNumber is SshMessageNumber.Ignore or SshMessageNumber.Debug or SshMessageNumber.Unimplemented;

    private async ValueTask<(SshKexInit KexInit, int PacketsBefore)> ReadClientKexInitAsync(CancellationToken cancellationToken)
    {
        var packetsBefore = 0;
        while (true)
        {
            var payload = await ReadMessageAsync(cancellationToken);
            if (payload[0] == SshMessageNumber.KeyExchangeInit)
            {
                return (SshKexInit.Parse(payload), packetsBefore);
            }

            if (!IsSkippable(payload[0]))
            {
                throw SshDisconnectRequiredException.ProtocolError($"The client sent SSH message {payload[0]} before its KEXINIT.");
            }

            packetsBefore++;
        }
    }

    // Reads up to the next key exchange method message (RFC 4253 section 7: nothing else
    // may come between KEXINIT and NEWKEYS but the generic transport messages).
    private async ValueTask<byte[]> ReadKeyExchangeMessageAsync(bool strict, CancellationToken cancellationToken)
    {
        while (true)
        {
            var payload = await ReadMessageAsync(cancellationToken);
            if (payload[0] is >= SshMessageNumber.FirstKeyExchangeMethodMessage and <= SshMessageNumber.LastKeyExchangeMethodMessage)
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
