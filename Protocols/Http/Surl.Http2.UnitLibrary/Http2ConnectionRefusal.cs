using Surl.Hpack;
using Surl.Protocol.Abstractions;

namespace Surl.Http2;

/// <summary>
/// Refuses an HTTP/2 connection past a connection limit (ADR-0081, decision 5, the "Too many
/// connections" row): reads the client's frames up to its first <c>HEADERS</c>, then writes an
/// empty <c>SETTINGS</c>, the refusal's header block on that stream with <c>END_STREAM</c>, and
/// <c>GOAWAY</c> naming that stream, <c>NO_ERROR</c>, and half-closes the connection.
/// </summary>
/// <remarks>
/// The server's <c>SETTINGS</c> is empty because no stream is served: curl accepts an empty one
/// (ADR-0081, decision 5, measured). The client's frames before its first <c>HEADERS</c> -
/// its <c>SETTINGS</c>, <c>WINDOW_UPDATE</c>, <c>PRIORITY</c>, an unknown type - are read and
/// passed over, and its <c>SETTINGS</c> is not acknowledged, since the connection ends with the
/// <c>GOAWAY</c>. A client that closes, or sends a frame RFC 9113 calls malformed, before any
/// <c>HEADERS</c> gets no bytes at all; so does one whose <c>HEADERS</c> does not arrive before the
/// caller's deadline cuts the read off.
/// </remarks>
public static class Http2ConnectionRefusal
{
    /// <summary>
    /// Reads <paramref name="connection"/>'s frames, the connection preface already read, up to the
    /// client's first <c>HEADERS</c> and answers that stream with <paramref name="fields"/>.
    /// </summary>
    /// <param name="connection">The connection, positioned just after the client's connection preface.</param>
    /// <param name="fields">The refusal's header fields, pseudo-header fields first.</param>
    /// <param name="cancellationToken">The refusal's deadline: cuts the read or the write off.</param>
    /// <returns>A task that completes when the refusal is written, or the client closed with no <c>HEADERS</c>.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> cut the refusal off.</exception>
    public static async Task WriteAsync(IConnection connection, IReadOnlyList<HpackHeaderField> fields, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(fields);

        if (await ReadFirstRequestStreamIdAsync(new Http2FrameReader(connection, Http2FrameReader.DefaultMaxFrameSize), cancellationToken) is not { } streamId)
        {
            return;
        }

        var writer = new Http2FrameWriter(connection);
        await writer.WriteAsync(new Http2SettingsFrame(false, []), cancellationToken);
        await writer.WriteHeaderBlockAsync(streamId, fields, endStream: true, cancellationToken);
        await writer.WriteAsync(new Http2GoAwayFrame(streamId, Http2ErrorCode.NoError, []), cancellationToken);
        await connection.CompleteWritesAsync(cancellationToken);
    }

    // The stream the client's first HEADERS opens; null when the client closed, or sent a
    // malformed frame, first.
    private static async Task<int?> ReadFirstRequestStreamIdAsync(Http2FrameReader reader, CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await reader.ReadFrameAsync(cancellationToken);
            if (result.Frame is Http2HeadersFrame headers)
            {
                return headers.StreamId;
            }

            if (result.Outcome is not (Http2FrameReadOutcome.FrameRead or Http2FrameReadOutcome.UnknownFrameSkipped))
            {
                return null;
            }
        }
    }
}
