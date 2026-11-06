namespace Surl.Compression;

/// <summary>
/// What a decoder returns: its <see cref="DecompressionOutcome"/>, the decompressed bytes when it
/// is <see cref="DecompressionOutcome.Decompressed"/>, and otherwise the reason it stopped.
/// </summary>
public sealed class DecompressionResult
{
    private DecompressionResult(DecompressionOutcome outcome, byte[] output, string reason)
    {
        Outcome = outcome;
        Output = output;
        Reason = reason;
    }

    /// <summary>How decoding ended.</summary>
    public DecompressionOutcome Outcome { get; }

    /// <summary>The decompressed bytes; empty unless <see cref="Outcome"/> is <see cref="DecompressionOutcome.Decompressed"/>.</summary>
    public byte[] Output { get; }

    /// <summary>Why decoding stopped, in a phrase; empty when it is <see cref="DecompressionOutcome.Decompressed"/>.</summary>
    public string Reason { get; }

    /// <summary>A result holding the decompressed bytes.</summary>
    internal static DecompressionResult Decompressed(byte[] output) =>
        new(DecompressionOutcome.Decompressed, output, string.Empty);

    /// <summary>A result naming why decoding stopped.</summary>
    internal static DecompressionResult Refused(DecompressionOutcome outcome, string reason) =>
        new(outcome, [], reason);
}
