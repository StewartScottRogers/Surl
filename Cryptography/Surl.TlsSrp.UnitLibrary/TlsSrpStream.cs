namespace Surl.TlsSrp;

/// <summary>
/// The application data of a completed TLS-SRP handshake, in plaintext (ADR-0122 decision 5). Reads
/// return the client's <c>application_data</c> records; a <c>close_notify</c> or the connection's
/// end is the end of the stream. A ClientHello after the handshake gets the warning
/// <c>no_renegotiation</c> and the connection carries on (RFC 5246 section 7.2.2); a warning alert
/// other than <c>close_notify</c> is ignored; a fatal alert is a <see cref="TlsPeerAlertException"/>;
/// a fault in a record (a bad MAC, a stray <c>ChangeCipherSpec</c>) sends its fatal alert and throws
/// the <see cref="TlsAlertException"/>. Writes go out as <c>application_data</c> records.
/// The stream is asynchronous only: the synchronous <see cref="Read(byte[], int, int)"/> and
/// <see cref="Write(byte[], int, int)"/> throw <see cref="NotSupportedException"/>. Disposing it
/// asynchronously sends <c>close_notify</c> first; either way it closes the connection.
/// </summary>
public sealed class TlsSrpStream : Stream
{
    private readonly Stream transport;
    private readonly TlsRecordReader reader;
    private readonly TlsRecordWriter writer;
    private readonly IReadOnlyList<IDisposable> protections;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private byte[] received = [];
    private int receivedOffset;
    private bool ended;
    private bool disposed;

    internal TlsSrpStream(Stream transport, TlsRecordReader reader, TlsRecordWriter writer, IReadOnlyList<IDisposable> protections)
    {
        this.transport = transport;
        this.reader = reader;
        this.writer = writer;
        this.protections = protections;
    }

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
        if (buffer.IsEmpty)
        {
            return;
        }

        await WriteRecordAsync(TlsContentType.ApplicationData, buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>Not supported: the stream is asynchronous only.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("TlsSrpStream is asynchronous only; use ReadAsync.");

    /// <summary>Not supported: the stream is asynchronous only.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("TlsSrpStream is asynchronous only; use WriteAsync.");

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

    /// <summary>Sends <c>close_notify</c> (unless the connection is already gone), then closes the connection.</summary>
    /// <returns>A task that completes once the connection is closed.</returns>
    public override async ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            await TrySendAlertAsync(writer, TlsAlerts.Warning(TlsAlertDescription.CloseNotify), CancellationToken.None).ConfigureAwait(false);
        }

        Dispose();
    }

    /// <summary>
    /// Writes an alert record, ignoring a connection that is already gone: the alert is the last
    /// thing sent, so there is nobody to tell that it failed.
    /// </summary>
    internal static async Task TrySendAlertAsync(TlsRecordWriter writer, byte[] alert, CancellationToken cancellationToken)
    {
        try
        {
            await writer.WriteAsync(TlsContentType.Alert, alert, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Closes the connection without sending <c>close_notify</c>.</summary>
    /// <param name="disposing">Unused: the class has no finalizer.</param>
    protected override void Dispose(bool disposing)
    {
        disposed = true;
        transport.Dispose();
        foreach (var protection in protections)
        {
            protection.Dispose();
        }

        writeLock.Dispose();
        base.Dispose(disposing);
    }

    private async Task ReceiveRecordAsync(CancellationToken cancellationToken)
    {
        var receiving = ReadAndAcceptRecordAsync(cancellationToken);
        TlsAlertException failure;
        try
        {
            await receiving.ConfigureAwait(false);
            return;
        }
        catch (TlsAlertException alert)
        {
            failure = alert;
        }

        await TrySendAlertAsync(writer, TlsAlerts.Fatal(failure.Description), cancellationToken).ConfigureAwait(false);
        await receiving.ConfigureAwait(false); // Throws the alert again, with its own stack.
    }

    private async Task ReadAndAcceptRecordAsync(CancellationToken cancellationToken)
    {
        var record = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        switch (record?.ContentType)
        {
            case null:
                ended = true;
                break;
            case TlsContentType.ApplicationData:
                received = record.Fragment;
                receivedOffset = 0;
                break;
            case TlsContentType.Alert:
                AcceptAlert(TlsAlerts.ReadPeerAlert(record.Fragment));
                break;
            case TlsContentType.Handshake:
                await WriteRecordAsync(TlsContentType.Alert, TlsAlerts.Warning(TlsAlertDescription.NoRenegotiation), cancellationToken)
                    .ConfigureAwait(false);
                break;
            default:
                throw new TlsAlertException(TlsAlertDescription.UnexpectedMessage, "The client sent ChangeCipherSpec after the handshake.");
        }
    }

    private void AcceptAlert(TlsPeerAlertException alert)
    {
        if (alert.IsFatal)
        {
            throw alert;
        }

        ended |= alert.Description == (byte)TlsAlertDescription.CloseNotify;
    }

    private async Task WriteRecordAsync(TlsContentType contentType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await writer.WriteAsync(contentType, payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
