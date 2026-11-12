using Surl.Content;

namespace Surl.HttpServing;

/// <summary>
/// The body of an <see cref="HttpAnswer"/>: none, bytes, a range of a content-store file, or a body written
/// while it is produced.
/// </summary>
public abstract record HttpAnswerBody
{
    /// <summary>
    /// No body: the answer to a <c>HEAD</c>, and every empty response.
    /// </summary>
    public static readonly HttpAnswerBody None = new NoBody();

    private HttpAnswerBody()
    {
    }

    /// <summary>
    /// A body of bytes held in memory.
    /// </summary>
    /// <param name="Content">The body's bytes.</param>
    public sealed record Bytes(ReadOnlyMemory<byte> Content) : HttpAnswerBody;

    /// <summary>
    /// A range of a file in a content store, copied from it as it is sent.
    /// </summary>
    /// <param name="Store">The content store the file is copied from.</param>
    /// <param name="Mapping">Where the file is.</param>
    /// <param name="Range">Which of its bytes are sent.</param>
    public sealed record ContentFile(ContentStore Store, ContentPathMapping Mapping, ContentByteRange Range) : HttpAnswerBody;

    /// <summary>
    /// A body written while it is produced, after the head has gone out: over HTTP/1.1 it is sent with
    /// <c>Transfer-Encoding: chunked</c>, each piece one chunk and the last chunk after <see cref="WriteAsync"/>
    /// completes (RFC 9112 section 7.1); over HTTP/1.0 it is sent as it is and the connection closes to end it
    /// (RFC 9112 section 6.3). The answer's fields carry no <c>Content-Length</c> and no
    /// <c>Transfer-Encoding</c>: the writer adds the framing. Only the HTTP/1.x writer
    /// (<see cref="Http1ConnectionServer"/>) sends one.
    /// </summary>
    /// <param name="WriteAsync">
    /// Produces the body, handing each piece to the writer it is given; the token is the exchange's.
    /// </param>
    public sealed record Streamed(Func<HttpAnswerChunkWriter, CancellationToken, Task> WriteAsync) : HttpAnswerBody;

    private sealed record NoBody : HttpAnswerBody;
}
