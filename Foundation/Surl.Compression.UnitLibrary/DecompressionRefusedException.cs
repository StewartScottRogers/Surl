namespace Surl.Compression;

/// <summary>
/// Thrown inside a decoder the moment it must stop, and caught by that decoder's public method,
/// which turns it into a <see cref="DecompressionResult"/>; it never leaves the library.
/// </summary>
internal sealed class DecompressionRefusedException(DecompressionOutcome outcome, string reason) : Exception(reason)
{
    /// <summary>Why the decoder stopped.</summary>
    public DecompressionOutcome Outcome { get; } = outcome;

    /// <summary>The refusal as the result a decoder returns.</summary>
    public DecompressionResult ToResult() => DecompressionResult.Refused(Outcome, Message);

    /// <summary>A refusal for input that breaks its format.</summary>
    public static DecompressionRefusedException Malformed(string reason) => new(DecompressionOutcome.Malformed, reason);
}
