using Surl.Content;

namespace Surl.Protocol.Http;

/// <summary>
/// The body of an <see cref="HttpAnswer"/>: none, bytes, or a range of a content-store file.
/// </summary>
internal abstract record HttpAnswerBody
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
    /// A range of a file in the content store, copied from it as it is sent.
    /// </summary>
    /// <param name="Mapping">Where the file is.</param>
    /// <param name="Range">Which of its bytes are sent.</param>
    public sealed record ContentFile(ContentPathMapping Mapping, ContentByteRange Range) : HttpAnswerBody;

    private sealed record NoBody : HttpAnswerBody;
}
