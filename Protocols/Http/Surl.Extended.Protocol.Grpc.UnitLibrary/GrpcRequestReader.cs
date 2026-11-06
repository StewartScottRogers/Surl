using Surl.Http2;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Reads a call's request messages from its HTTP/2 stream's <c>DATA</c> (ADR-0116 decisions 6 and 8):
/// reassembled by <see cref="LengthPrefixedMessageReader" />, judged against the limits, and
/// decompressed under the call's <c>grpc-encoding</c>.
/// </summary>
internal sealed class GrpcRequestReader
{
    private readonly Http2RequestStream stream;
    private readonly GrpcMessageEncoding encoding;
    private readonly int maximumMessageLength;
    private readonly LengthPrefixedMessageReader messages;
    private bool hasBodyEnded;

    /// <summary>Creates a reader of <paramref name="stream" />'s request messages.</summary>
    /// <param name="stream">The call's stream.</param>
    /// <param name="encoding">The call's <c>grpc-encoding</c>.</param>
    /// <param name="maximumMessageLength">The longest message, framed or decompressed.</param>
    /// <param name="maximumCallLength">The most bytes the call's messages may hold together.</param>
    public GrpcRequestReader(Http2RequestStream stream, GrpcMessageEncoding encoding, int maximumMessageLength, long maximumCallLength)
    {
        this.stream = stream;
        this.encoding = encoding;
        this.maximumMessageLength = maximumMessageLength;
        messages = new LengthPrefixedMessageReader(maximumMessageLength, maximumCallLength);
    }

    /// <summary>Whether the client reset the stream or closed the connection before half-closing it.</summary>
    public bool HasClientLeft => stream.HasRequestBodyEndedEarly;

    /// <summary>Reads the next request message's payload.</summary>
    /// <param name="cancellationToken">Cuts the wait off.</param>
    /// <returns>The payload, or <see langword="null" /> once the client half-closed between messages.</returns>
    /// <exception cref="GrpcCallException">The message breaks a framing, compression or limit rule, or the body ended inside one.</exception>
    /// <exception cref="OperationCanceledException">The client reset the stream or closed the connection before half-closing.</exception>
    public async Task<ReadOnlyMemory<byte>?> ReadAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (messages.TryRead(out var message))
            {
                return GrpcMessageDecompression.ReadPayload(message, encoding, maximumMessageLength);
            }

            if (hasBodyEnded)
            {
                messages.Complete();
                return null;
            }

            await ReadPieceAsync(cancellationToken);
        }
    }

    private async Task ReadPieceAsync(CancellationToken cancellationToken)
    {
        var piece = await stream.ReadBodyAsync(cancellationToken);
        if (piece is not null)
        {
            messages.Append(piece);
            return;
        }

        if (HasClientLeft)
        {
            throw new OperationCanceledException("The client ended the stream before half-closing it.");
        }

        hasBodyEnded = true;
    }
}
