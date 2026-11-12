using Surl.Content;

namespace Surl.HttpServing;

/// <summary>
/// The body of an <see cref="HttpAnswer"/>: none, bytes, a range of a content-store file, several
/// ranges of one with bytes between them, or a body written while it is produced.
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

    /// <summary>
    /// Several ranges of one content-store file, each after bytes of its own, then closing
    /// bytes: the shape of a <c>multipart/byteranges</c> body (RFC 9110, section 14.6), whose
    /// delimiters and part fields the answerer writes into <see cref="ContentFilePart.Preamble"/>
    /// and <see cref="Epilogue"/>. The writers copy it as it is given and know nothing of MIME.
    /// </summary>
    /// <param name="Store">The content store the file is copied from.</param>
    /// <param name="Mapping">Where the file is.</param>
    /// <param name="Parts">The parts, in the order sent.</param>
    /// <param name="Epilogue">The bytes sent after the last part.</param>
    public sealed record ContentFileParts(ContentStore Store, ContentPathMapping Mapping, IReadOnlyList<ContentFilePart> Parts, ReadOnlyMemory<byte> Epilogue) : HttpAnswerBody
    {
        /// <summary>
        /// The body's length: every preamble, every range and the epilogue.
        /// </summary>
        public long Length => FileByteCount + Parts.Sum(part => (long)part.Preamble.Length) + Epilogue.Length;

        /// <summary>
        /// How many of the body's bytes are the file's: every range's byte count.
        /// </summary>
        public long FileByteCount => Parts.Sum(part => part.Range.ByteCount);

        /// <summary>
        /// The body of one range of a file as parts: the range alone, with no preamble and no
        /// epilogue, so every writer sends both kinds of file body one way.
        /// </summary>
        /// <param name="file">The one range of a file.</param>
        /// <returns>One part holding <paramref name="file"/>'s range.</returns>
        public static ContentFileParts Of(ContentFile file) =>
            new(file.Store, file.Mapping, [new ContentFilePart(ReadOnlyMemory<byte>.Empty, file.Range)], ReadOnlyMemory<byte>.Empty);

        /// <summary>
        /// Writes each part's preamble then copies its range, then writes the epilogue, stopping
        /// at the first range the file could not give whole.
        /// </summary>
        /// <param name="destination">Where the body is written.</param>
        /// <param name="cancellationToken">Cancels the copy.</param>
        /// <returns>How many of the file's bytes were copied; less than
        /// <see cref="FileByteCount"/> when the file shrank while it was sent.</returns>
        public async Task<long> CopyToAsync(Stream destination, CancellationToken cancellationToken)
        {
            long copied = 0;
            foreach (var part in Parts)
            {
                await destination.WriteAsync(part.Preamble, cancellationToken);
                var partCopied = await Store.CopyFileBytesAsync(Mapping, part.Range, destination, cancellationToken);
                copied += partCopied;
                if (partCopied < part.Range.ByteCount)
                {
                    return copied;
                }
            }

            await destination.WriteAsync(Epilogue, cancellationToken);

            return copied;
        }
    }

    private sealed record NoBody : HttpAnswerBody;
}
