namespace Surl.Protocol.Abstractions;

/// <summary>
/// The chunk signatures of an AWS Signature Version 4 <c>aws-chunked</c> body signed with
/// <c>x-amz-content-sha256: STREAMING-AWS4-HMAC-SHA256-PAYLOAD</c> ("Signature Calculations for
/// the Authorization Header: Transferring Payload in Multiple Chunks"; ADR-0108 decision 4.5):
/// each chunk's signature is chained from the one before it, starting at the request's own
/// (seed) signature. It rides on an accepted <see cref="HttpAuthenticationVerdict"/> as
/// <see cref="HttpAuthenticationVerdict.ChunkSignatures"/>; the server decodes the body and asks
/// it once per chunk, in order, the final empty chunk included.
/// </summary>
public interface IHttpChunkSignatureChain
{
    /// <summary>
    /// Checks the next chunk's signature. When it matches, the chain moves on to that chunk, so
    /// the following chunk is checked against it; when it does not, the chain stays where it was.
    /// </summary>
    /// <param name="chunkSha256">The SHA-256 of the chunk's data, the empty data's for the final chunk.</param>
    /// <param name="chunkSignature">The <c>chunk-signature</c> the chunk's header carries, as sent.</param>
    /// <returns><see langword="true"/> when the signature is the one the chain expects.</returns>
    bool VerifyNextChunk(ReadOnlySpan<byte> chunkSha256, string chunkSignature);

    /// <summary>
    /// Checks the <c>x-amz-trailer-signature</c> of a body sent with
    /// <c>x-amz-content-sha256: STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER</c>, once the final
    /// empty chunk has been checked: it is chained from that chunk's signature.
    /// </summary>
    /// <param name="trailerSha256">The SHA-256 of the trailer's lines, each as <c>name:value</c> ended by a line feed, the signature line left out.</param>
    /// <param name="trailerSignature">The signature the <c>x-amz-trailer-signature</c> line carries, as sent.</param>
    /// <returns><see langword="true"/> when the signature is the one the chain expects.</returns>
    bool VerifyTrailer(ReadOnlySpan<byte> trailerSha256, string trailerSignature);
}
