namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The rules a block ID keeps ("Put Block"): base64 of at least one and at most 64 bytes, and as
/// long as every other block ID of the same blob.
/// </summary>
internal static class AzureBlockId
{
    /// <summary>
    /// The most bytes a block ID holds before base64.
    /// </summary>
    public const int MaxDecodedBytes = 64;

    /// <summary>
    /// Whether <paramref name="id"/> is base64 of 1 to <see cref="MaxDecodedBytes"/> bytes.
    /// </summary>
    /// <param name="id">The block ID as sent, percent-decoded.</param>
    /// <returns>Whether it is well formed.</returns>
    public static bool IsWellFormed(string id) =>
        Convert.TryFromBase64String(id, new byte[MaxDecodedBytes], out var written) && written > 0;

    /// <summary>
    /// Whether <paramref name="id"/> is as long as each of <paramref name="blocks"/>' IDs.
    /// </summary>
    /// <param name="id">The new block ID.</param>
    /// <param name="blocks">The blob's other blocks.</param>
    /// <returns>Whether the lengths agree.</returns>
    public static bool MatchesLengthOf(string id, IEnumerable<AzureBlock> blocks) =>
        blocks.All(block => block.Id.Length == id.Length);
}
