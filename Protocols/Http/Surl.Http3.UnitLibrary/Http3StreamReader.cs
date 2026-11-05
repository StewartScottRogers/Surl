using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// Reads one of the client's QUIC streams as HTTP/3 does (RFC 9114 section 7.1): variable-length
/// integers, frame headers and payloads, buffered so a frame may arrive in any number of pieces.
/// </summary>
/// <remarks>
/// Only the client's <c>FIN</c> between frames ends a stream cleanly. One that ends with
/// <c>FIN</c> part way through an integer or a payload has broken a frame, <c>H3_FRAME_ERROR</c>,
/// thrown as an <see cref="Http3ConnectionException"/>; one the client resets, or whose connection
/// ends, anywhere - the reset discards what was not read yet - is an
/// <see cref="Http3StreamAbandonedException"/>.
/// </remarks>
/// <param name="connection">The connection the stream belongs to.</param>
/// <param name="stream">The stream, opened by the client.</param>
internal sealed class Http3StreamReader(QuicApplicationConnection connection, QuicStream stream)
{
    private readonly byte[] buffer = new byte[16384];
    private int start;
    private int end;

    /// <summary>The stream's ID.</summary>
    public ulong StreamId => stream.Id;

    /// <summary>The stream read.</summary>
    public QuicStream Stream => stream;

    /// <summary>Waits for at least one more byte of the stream.</summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns><see langword="false"/> once the stream has ended (its <c>FIN</c>, a reset, or the connection's end) with nothing left to read.</returns>
    public async ValueTask<bool> HasMoreAsync(CancellationToken cancellationToken)
    {
        if (start < end)
        {
            return true;
        }

        start = 0;
        end = await connection.ReadAsync(stream, buffer, cancellationToken);
        return end > 0;
    }

    /// <summary>Reads a frame's type and length, or null when the client's <c>FIN</c> ended the stream between frames.</summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The frame header, or null at the stream's end.</returns>
    public async ValueTask<Http3FrameHeader?> ReadFrameHeaderAsync(CancellationToken cancellationToken)
    {
        if (await ReadIntegerOrEndAsync(cancellationToken) is not { } type)
        {
            return null;
        }

        return new Http3FrameHeader(type, await ReadIntegerAsync(cancellationToken));
    }

    /// <summary>Reads a variable-length integer, or null when the client's <c>FIN</c> ended the stream before its first byte.</summary>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The integer, or null.</returns>
    /// <exception cref="Http3StreamAbandonedException">The client reset the stream, or the connection ended, before its first byte.</exception>
    public async ValueTask<ulong?> ReadIntegerOrEndAsync(CancellationToken cancellationToken) =>
        await HasMoreBeforeFinAsync(cancellationToken) ? await ReadIntegerAsync(cancellationToken) : null;

    /// <summary>Waits for at least one more byte of the stream, or for the client's <c>FIN</c>.</summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns><see langword="false"/> once the client's <c>FIN</c> has ended the stream with nothing left to read.</returns>
    /// <exception cref="Http3StreamAbandonedException">The client reset the stream, or the connection ended, with nothing left to read.</exception>
    public async ValueTask<bool> HasMoreBeforeFinAsync(CancellationToken cancellationToken) =>
        await HasMoreAsync(cancellationToken) || (stream.IsReadComplete ? false : throw new Http3StreamAbandonedException(stream.Id));

    /// <summary>Reads exactly <paramref name="count"/> bytes.</summary>
    /// <param name="count">How many.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The bytes.</returns>
    public async ValueTask<byte[]> ReadExactlyAsync(int count, CancellationToken cancellationToken)
    {
        var result = new byte[count];
        var filled = 0;
        while (filled < count)
        {
            var piece = await ReadPieceAsync(count - filled, cancellationToken);
            piece.CopyTo(result.AsSpan(filled));
            filled += piece.Length;
        }

        return result;
    }

    /// <summary>Reads between 1 and <paramref name="most"/> bytes: what is buffered, or the next piece to arrive.</summary>
    /// <param name="most">The most bytes to take; at least 1.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>The bytes.</returns>
    public async ValueTask<byte[]> ReadPieceAsync(long most, CancellationToken cancellationToken)
    {
        await RequireMoreAsync(cancellationToken);
        var count = (int)Math.Min(most, end - start);
        var piece = buffer[start..(start + count)];
        start += count;
        return piece;
    }

    /// <summary>Reads and throws away <paramref name="count"/> bytes: an ignored frame's payload.</summary>
    /// <param name="count">How many.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>A task that completes once they are read.</returns>
    public async ValueTask SkipAsync(ulong count, CancellationToken cancellationToken)
    {
        while (count > 0)
        {
            count -= (ulong)(await ReadPieceAsync((long)Math.Min(count, int.MaxValue), cancellationToken)).Length;
        }
    }

    private async ValueTask<ulong> ReadIntegerAsync(CancellationToken cancellationToken)
    {
        await RequireMoreAsync(cancellationToken);
        var bytes = await ReadExactlyAsync(QuicVariableLengthInteger.LengthFromFirstByte(buffer[start]), cancellationToken);
        QuicVariableLengthInteger.TryRead(bytes, out var value, out _);
        return value;
    }

    private async ValueTask RequireMoreAsync(CancellationToken cancellationToken)
    {
        if (!await HasMoreBeforeFinAsync(cancellationToken))
        {
            throw new Http3ConnectionException(Http3ErrorCode.FrameError, $"stream {stream.Id} ended part way through a frame");
        }
    }
}
