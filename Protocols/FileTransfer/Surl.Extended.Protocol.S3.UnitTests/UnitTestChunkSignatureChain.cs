using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// A hand-written chain for the S3 server's tests: it expects the given chunks in order, each by
/// the SHA-256 of its data and its signature, and accepts a chunk only when both are the next
/// expected ones, as <c>Surl.Authentication</c>'s chain accepts only the signature the chain
/// computes over that data. Every chunk asked about is recorded.
/// </summary>
/// <param name="expected">The chunks expected, in order.</param>
internal sealed class UnitTestChunkSignatureChain(params (byte[] DataSha256, string Signature)[] expected) : IHttpChunkSignatureChain
{
    private int next;

    public List<(byte[] DataSha256, string Signature)> Asked { get; } = [];

    public bool VerifyNextChunk(ReadOnlySpan<byte> chunkSha256, string chunkSignature)
    {
        Asked.Add((chunkSha256.ToArray(), chunkSignature));
        var isNext = next < expected.Length
            && chunkSha256.SequenceEqual(expected[next].DataSha256)
            && chunkSignature == expected[next].Signature;
        next += isNext ? 1 : 0;

        return isNext;
    }
}
