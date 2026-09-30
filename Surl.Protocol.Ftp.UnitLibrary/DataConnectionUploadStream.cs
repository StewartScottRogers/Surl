using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// The upload of one <c>STOR</c> or <c>APPE</c>, read from its data connection: the data
/// connection is opened, and the <c>150</c> reply sent, only at the first read, so a content
/// store that refuses the upload before reading it refuses it before any data connection is
/// used (ADR-0052, decision 8).
/// </summary>
/// <remarks>
/// A read after the data connection could not be opened throws
/// <see cref="FtpDataConnectionNotOpenedException"/>. A read the data connection fails with
/// <see cref="IOException"/> sets <see cref="ConnectionReadFailed"/>, so the caller can tell
/// the client going away from the file system failing. Disposing it disposes the data
/// connection, if one was opened.
/// </remarks>
internal sealed class DataConnectionUploadStream : Stream
{
    private readonly FtpDataConnections dataConnections;
    private readonly Func<ValueTask> sendOpeningReplyAsync;
    private IConnection? connection;

    /// <summary>
    /// Creates the stream; nothing is opened until the first read.
    /// </summary>
    /// <param name="dataConnections">The data connection the session prepared.</param>
    /// <param name="sendOpeningReplyAsync">Sends the <c>150</c> reply once the data connection is open.</param>
    public DataConnectionUploadStream(FtpDataConnections dataConnections, Func<ValueTask> sendOpeningReplyAsync)
    {
        this.dataConnections = dataConnections;
        this.sendOpeningReplyAsync = sendOpeningReplyAsync;
    }

    /// <summary>
    /// Whether a read from the data connection failed with <see cref="IOException"/>.
    /// </summary>
    public bool ConnectionReadFailed { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException("A connection has no length.");

    public override long Position
    {
        get => throw new NotSupportedException("A connection has no position.");
        set => throw new NotSupportedException("A connection has no position.");
    }

    /// <summary>
    /// Resets the data connection, if one was opened, so the client never takes a refused or
    /// broken upload for a finished one.
    /// </summary>
    public void AbortConnection() => connection?.Abort();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        connection ??= await OpenAsync();
        try
        {
            return await connection.ReadAsync(buffer, cancellationToken);
        }
        catch (IOException)
        {
            ConnectionReadFailed = true;
            throw;
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("Read from a connection asynchronously.");

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("A connection cannot seek.");

    public override void SetLength(long value) =>
        throw new NotSupportedException("A connection has no length.");

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The stream is read-only.");

    private async ValueTask<IConnection> OpenAsync()
    {
        var opened = await dataConnections.OpenAsync() ?? throw new FtpDataConnectionNotOpenedException();
        await sendOpeningReplyAsync();
        return opened;
    }
}
