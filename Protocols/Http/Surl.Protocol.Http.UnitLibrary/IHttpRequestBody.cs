using System.Security.Cryptography;

namespace Surl.Protocol.Http;

/// <summary>
/// The body of an <see cref="HttpRequest"/>, as the HTTP version that carried it frames it:
/// what was announced of it, and a way to read it to its end and throw its bytes away.
/// </summary>
internal interface IHttpRequestBody
{
    /// <summary>
    /// Whether the request still has a body to read: one was announced and has not been read.
    /// </summary>
    bool IsAnnounced { get; }

    /// <summary>
    /// The body's length as announced before any byte of it is read, or <see langword="null"/>
    /// when none was announced.
    /// </summary>
    long? AnnouncedLength { get; }

    /// <summary>
    /// Whether the server can read the body to its end; one it cannot is never read.
    /// </summary>
    bool IsReadable { get; }

    /// <summary>
    /// Reads the body to its end and throws its bytes away, hashing each into
    /// <paramref name="bodyHash"/> first when one is given. Once it is discarded,
    /// <see cref="IsAnnounced"/> is <see langword="false"/>. Never called when
    /// <see cref="IsReadable"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="bodyHash">Where the body's bytes are hashed, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cuts the read off.</param>
    /// <returns>How the read ended.</returns>
    Task<HttpRequestBodyDiscardOutcome> DiscardAsync(IncrementalHash? bodyHash, CancellationToken cancellationToken);
}
