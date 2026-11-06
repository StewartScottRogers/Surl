namespace Surl.TlsSrp;

/// <summary>
/// Reads the client's handshake messages from its records during the handshake (RFC 5246 sections
/// 6.2.1 and 7.4): a message may span records and a record may hold several. A record of another
/// type while a message is wanted, or a message other than the one the handshake expects next, is
/// <see cref="TlsAlertDescription.UnexpectedMessage"/>; any alert from the client is a
/// <see cref="TlsPeerAlertException"/>.
/// </summary>
/// <param name="records">The connection's record reader.</param>
internal sealed class TlsHandshakeMessageReader(TlsRecordReader records)
{
    /// <summary>The longest handshake message read: a ClientHello's fields cannot reach it.</summary>
    public const int MaxMessageLength = 1 << 17;

    private const int HeaderLength = 4;

    private byte[] pending = [];

    /// <summary>Reads the next handshake message and requires it to be <paramref name="expected"/>.</summary>
    /// <param name="expected">The message the handshake is waiting for.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The message.</returns>
    public async Task<TlsHandshakeMessage> ReadAsync(TlsHandshakeType expected, CancellationToken cancellationToken)
    {
        TlsHandshakeMessage? message;
        while ((message = TryTake()) is null)
        {
            var record = await ReadRecordAsync(cancellationToken).ConfigureAwait(false);
            RequireType(record, TlsContentType.Handshake);
            pending = [.. pending, .. record.Fragment];
        }

        if (message.Type != (byte)expected)
        {
            throw Unexpected($"The client sent handshake message {message.Type} where {expected} belongs.");
        }

        return message;
    }

    /// <summary>
    /// Reads the client's <c>ChangeCipherSpec</c>, which must start a record of its own after the
    /// last handshake message.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes once it is read.</returns>
    public async Task ReadChangeCipherSpecAsync(CancellationToken cancellationToken)
    {
        if (pending.Length > 0)
        {
            throw Unexpected("The client sent more handshake bytes where ChangeCipherSpec belongs.");
        }

        var record = await ReadRecordAsync(cancellationToken).ConfigureAwait(false);
        RequireType(record, TlsContentType.ChangeCipherSpec);
        if (record.Fragment is not [1])
        {
            throw new TlsAlertException(TlsAlertDescription.DecodeError, "The client's ChangeCipherSpec is not the one byte 1.");
        }
    }

    private async Task<TlsRecord> ReadRecordAsync(CancellationToken cancellationToken)
    {
        var record = await records.ReadAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new EndOfStreamException("The client closed the connection during the handshake.");
        if (record.ContentType == TlsContentType.Alert)
        {
            throw TlsAlerts.ReadPeerAlert(record.Fragment);
        }

        return record;
    }

    private TlsHandshakeMessage? TryTake()
    {
        if (pending.Length < HeaderLength)
        {
            return null;
        }

        var length = (pending[1] << 16) | (pending[2] << 8) | pending[3];
        if (length > MaxMessageLength)
        {
            throw new TlsAlertException(TlsAlertDescription.DecodeError, $"A handshake message of {length} bytes is over the limit.");
        }

        if (pending.Length < HeaderLength + length)
        {
            return null;
        }

        var framed = pending[..(HeaderLength + length)];
        pending = pending[framed.Length..];
        return new TlsHandshakeMessage(framed[0], framed[HeaderLength..], framed);
    }

    private static void RequireType(TlsRecord record, TlsContentType expected)
    {
        if (record.ContentType != expected)
        {
            throw Unexpected($"The client sent a {record.ContentType} record where {expected} belongs.");
        }
    }

    private static TlsAlertException Unexpected(string message) => new(TlsAlertDescription.UnexpectedMessage, message);
}
