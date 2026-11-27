using System.Security.Cryptography;
using Surl.Tls13;

namespace Surl.Tls13Stream;

/// <summary>
/// The application data of a TLS 1.3 connection whose server side <see cref="AcceptAsync"/> has
/// handshaken (RFC 8446), in plaintext. Reads return the client's application bytes; a
/// <c>close_notify</c> (which is answered with ours) or the transport's end is the end of the
/// stream. A <c>KeyUpdate</c> is honoured and answered when it asks for one; <c>user_canceled</c>
/// is ignored; any other alert is a <see cref="Tls13PeerAlertException"/>. A fault in a record (a
/// bad tag, a plaintext record, a handshake message that is not a KeyUpdate) sends its fatal alert
/// and throws the <see cref="TlsAlertException"/>. The stream is asynchronous only; disposing it
/// asynchronously sends <c>close_notify</c> first, unless it was sent already, and either way closes
/// the transport. There are no session tickets and no 0-RTT.
/// </summary>
public sealed class Tls13ServerStream : Stream
{
    private static readonly byte[] KeyUpdateHeader = [24, 0, 0, 1];

    private readonly Tls13Connection connection;
    private byte[] received = [];
    private int receivedOffset;
    private bool ended;
    private bool disposed;
    private bool closeNotifySent;

    private Tls13ServerStream(Tls13Connection connection, TlsServerHandshake handshake)
    {
        this.connection = connection;
        CipherSuite = handshake.CipherSuite!.Value;
        ApplicationProtocol = handshake.ApplicationProtocol;
        ClientHello = handshake.ClientHello!;
        ClientCertificateChain = handshake.ClientCertificateChain;
    }

    /// <summary>The negotiated cipher suite.</summary>
    public TlsCipherSuite CipherSuite { get; }

    /// <summary>The ALPN protocol agreed from the client's offer, or null when none was.</summary>
    public string? ApplicationProtocol { get; }

    /// <summary>The ClientHello the client sent.</summary>
    public ClientHello ClientHello { get; }

    /// <summary>The client's verified certificate chain when the options asked for one; otherwise null.</summary>
    public IReadOnlyList<ReadOnlyMemory<byte>>? ClientCertificateChain { get; }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>Runs the server side of a TLS 1.3 handshake over <paramref name="transport"/>.</summary>
    /// <param name="transport">The connection; the stream owns it once this returns, and the caller keeps it when this throws.</param>
    /// <param name="options">The certificate, key, ALPN protocols and client-certificate policy.</param>
    /// <param name="random">
    /// The source of the server random and of the chosen key share group's ML-KEM m, private key or
    /// exponent. A NIST curve key, alone or in a hybrid, does not come from it: the base class
    /// library generates it. The remarks on <see cref="TlsServerHandshake"/> give the draw order.
    /// </param>
    /// <param name="alreadyRead">Bytes already read from the transport, starting at a record header - typically the ClientHello record - replayed before anything more is read.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The stream carrying the connection's application data.</returns>
    /// <exception cref="TlsAlertException">The handshake failed; the fatal alert was written first.</exception>
    /// <exception cref="Tls13PeerAlertException">The client ended the handshake with an alert.</exception>
    /// <exception cref="EndOfStreamException">The connection ended during the handshake.</exception>
    public static async Task<Tls13ServerStream> AcceptAsync(
        Stream transport,
        TlsServerHandshakeOptions options,
        RandomNumberGenerator random,
        ReadOnlyMemory<byte> alreadyRead = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);
        var connection = new Tls13Connection(transport, new Tls13RecordReader(transport, alreadyRead));
        using var handshake = new TlsServerHandshake(options, random);
        await connection.WriteFatalAlertOnFailureAsync(
            Tls13ServerHandshakeRunner.RunAsync(connection, handshake, cancellationToken), cancellationToken).ConfigureAwait(false);
        return new Tls13ServerStream(connection, handshake);
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (!buffer.IsEmpty && receivedOffset == received.Length && !ended)
        {
            await ReceiveRecordAsync(cancellationToken).ConfigureAwait(false);
        }

        var count = Math.Min(buffer.Length, received.Length - receivedOffset);
        received.AsMemory(receivedOffset, count).CopyTo(buffer);
        receivedOffset += count;
        return count;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await connection.WriteAsync(TlsContentType.ApplicationData, buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>Not supported: the stream is asynchronous only.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Tls13ServerStream is asynchronous only; use ReadAsync.");

    /// <summary>Not supported: the stream is asynchronous only.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Tls13ServerStream is asynchronous only; use WriteAsync.");

    /// <summary>Does nothing: every write is flushed as it is made.</summary>
    public override void Flush()
    {
    }

    /// <summary>Does nothing: every write is flushed as it is made.</summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>A completed task.</returns>
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>
    /// Sends <c>close_notify</c> once, so the server's side of the stream ends while the connection
    /// stays open for the client's; nothing may be written after it. A second call, and a later
    /// <see cref="DisposeAsync"/>, send nothing more.
    /// </summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the alert is written.</returns>
    /// <exception cref="IOException">The connection is gone.</exception>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (closeNotifySent)
        {
            return;
        }

        closeNotifySent = true;
        await connection.WriteCloseNotifyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends <c>close_notify</c> (unless the connection is gone or it was sent already), then closes the connection.</summary>
    /// <returns>A task that completes once the connection is closed.</returns>
    public override async ValueTask DisposeAsync()
    {
        if (!disposed && !closeNotifySent)
        {
            closeNotifySent = true;
            await connection.TryWriteCloseNotifyAsync(CancellationToken.None).ConfigureAwait(false);
        }

        Dispose();
    }

    /// <summary>Closes the connection without sending <c>close_notify</c>.</summary>
    /// <param name="disposing">Unused: the class has no finalizer.</param>
    protected override void Dispose(bool disposing)
    {
        disposed = true;
        connection.Dispose();
        base.Dispose(disposing);
    }

    private Task ReceiveRecordAsync(CancellationToken cancellationToken) =>
        connection.WriteFatalAlertOnFailureAsync(AcceptRecordAsync(cancellationToken), cancellationToken);

    private async Task AcceptRecordAsync(CancellationToken cancellationToken)
    {
        var record = await connection.ReadAsync(skipCompatibilityChangeCipherSpec: false, cancellationToken).ConfigureAwait(false);
        switch (record?.ContentType)
        {
            case null:
                ended = true;
                break;
            case TlsContentType.ApplicationData:
                received = record.Content;
                receivedOffset = 0;
                break;
            case TlsContentType.Alert:
                await AcceptAlertAsync(record.Content, cancellationToken).ConfigureAwait(false);
                break;
            case TlsContentType.Handshake:
                await AcceptKeyUpdateAsync(record.Content, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new TlsAlertException(TlsAlert.UnexpectedMessage, "The client sent change_cipher_spec after the handshake.");
        }
    }

    private async Task AcceptAlertAsync(byte[] content, CancellationToken cancellationToken)
    {
        if (content.Length != 2)
        {
            throw new TlsAlertException(TlsAlert.DecodeError, "An alert is two bytes.");
        }

        var alert = (TlsAlert)content[1];
        if (alert == TlsAlert.CloseNotify)
        {
            ended = true;
            await AnswerCloseNotifyAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (alert != TlsAlert.UserCanceled)
        {
            throw new Tls13PeerAlertException(alert);
        }
    }

    private async Task AnswerCloseNotifyAsync(CancellationToken cancellationToken)
    {
        if (!closeNotifySent)
        {
            closeNotifySent = true;
            await connection.TryWriteCloseNotifyAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AcceptKeyUpdateAsync(byte[] content, CancellationToken cancellationToken)
    {
        if (content.Length != 5 || !content.AsSpan(0, 4).SequenceEqual(KeyUpdateHeader))
        {
            throw new TlsAlertException(TlsAlert.UnexpectedMessage, "After the handshake the client may send only a KeyUpdate (RFC 8446 section 4.6.3).");
        }

        if (content[4] > 1)
        {
            throw new TlsAlertException(TlsAlert.IllegalParameter, "KeyUpdate's request_update is 0 or 1.");
        }

        connection.UpdateReadKeys();
        if (content[4] == 1)
        {
            await connection.WriteKeyUpdateAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
