using Surl.Hpack;
using Surl.Http2;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// Writes one call's response on its HTTP/2 stream (PROTOCOL-HTTP2 "Responses"; ADR-0116 decision 4):
/// Response-Headers before the first reply, each reply as a Length-Prefixed-Message, then Trailers -
/// or, when no reply was written, Trailers-Only, one header block holding both.
/// </summary>
/// <param name="stream">The call's stream.</param>
/// <param name="contentType">The response's <c>content-type</c>.</param>
internal sealed class GrpcResponseWriter(Http2RequestStream stream, string contentType)
{
    /// <summary>Whether Response-Headers have been written, so the end is Trailers and not Trailers-Only.</summary>
    public bool HasWrittenHeaders { get; private set; }

    /// <summary>Writes one reply, after the Response-Headers when it is the first.</summary>
    /// <param name="payload">The reply's protobuf bytes.</param>
    /// <returns>A task that completes when the reply is written.</returns>
    public async Task WriteReplyAsync(ReadOnlyMemory<byte> payload)
    {
        if (!HasWrittenHeaders)
        {
            HasWrittenHeaders = true;
            await stream.WriteHeadersAsync(
                [new(":status", "200"), new("content-type", contentType), new("grpc-accept-encoding", GrpcMessageDecompression.AcceptEncodingValue)],
                endStream: false);
        }

        await stream.WriteDataAsync(LengthPrefixedMessage.WriteUncompressed(payload.Span), endStream: false);
    }

    /// <summary>
    /// Ends the call with <paramref name="status" />: Trailers after a reply, else Trailers-Only, either
    /// ending the stream.
    /// </summary>
    /// <param name="status">The status; its <c>grpc-message</c> is sent when its text is not empty.</param>
    /// <param name="metadata">Metadata sent after the status fields.</param>
    /// <returns>A task that completes when the block is written.</returns>
    public Task FinishAsync(GrpcStatus status, IReadOnlyList<KeyValuePair<string, string>> metadata)
    {
        var fields = new List<HpackHeaderField>();
        if (!HasWrittenHeaders)
        {
            fields.Add(new(":status", "200"));
            fields.Add(new("content-type", contentType));
        }

        fields.Add(new("grpc-status", status.StatusValue));
        if (status.Message.Length > 0)
        {
            fields.Add(new("grpc-message", status.MessageValue));
        }

        if (!HasWrittenHeaders)
        {
            fields.Add(new("grpc-accept-encoding", GrpcMessageDecompression.AcceptEncodingValue));
        }

        fields.AddRange(metadata.Select(field => new HpackHeaderField(field.Key, field.Value)));
        return stream.WriteHeadersAsync(fields, endStream: true);
    }
}
