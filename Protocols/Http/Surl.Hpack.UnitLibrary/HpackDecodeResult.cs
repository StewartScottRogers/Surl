namespace Surl.Hpack;

/// <summary>What <see cref="HpackDecoder.Decode" /> made of one header block.</summary>
/// <param name="Fields">The header list in order, or empty when the block did not decode.</param>
/// <param name="Error">Why the block did not decode, or <see langword="null" /> when it did.</param>
public sealed record HpackDecodeResult(IReadOnlyList<HpackHeaderField> Fields, HpackDecodingError? Error)
{
    /// <summary>Gets whether the block decoded.</summary>
    public bool IsDecoded => Error is null;
}
