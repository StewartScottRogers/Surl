using Surl.Protocol.Abstractions;

namespace Surl.Authentication;

/// <summary>
/// The chunk signatures of one <c>STREAMING-AWS4-HMAC-SHA256-PAYLOAD</c> body ("Signature
/// Calculations for the Authorization Header: Transferring Payload in Multiple Chunks"; ADR-0108
/// decision 4.5): the first chunk is signed over the request's verified (seed) signature, each
/// later one over the chunk before's, all under the account's signing key. Each signature is
/// compared in fixed time.
/// </summary>
/// <param name="authorization">The request's credentials, for the provider and the scope.</param>
/// <param name="requestTime">The request's date field as sent.</param>
/// <param name="signingKey">The account's signing key for the scope.</param>
/// <param name="secretComparer">Compares two signatures in fixed time.</param>
internal sealed class AwsSigV4ChunkSignatureChain(
    AwsSigV4Authorization authorization, string requestTime, byte[] signingKey, ISecretComparer secretComparer) : IHttpChunkSignatureChain
{
    private byte[] previousSignature = authorization.Signature;

    /// <inheritdoc/>
    public bool VerifyNextChunk(ReadOnlySpan<byte> chunkSha256, string chunkSignature)
    {
        ArgumentNullException.ThrowIfNull(chunkSignature);

        var expected = AwsSigV4Calculation.ComputeChunkSignature(authorization, requestTime, signingKey, previousSignature, chunkSha256);
        var sent = AwsSigV4Authorization.ReadSignature(chunkSignature);
        if (sent is null || !secretComparer.FixedTimeEquals(expected, sent))
        {
            return false;
        }

        previousSignature = expected;

        return true;
    }
}
