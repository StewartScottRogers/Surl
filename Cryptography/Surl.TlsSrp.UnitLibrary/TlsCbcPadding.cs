namespace Surl.TlsSrp;

/// <summary>
/// The padding of a TLS 1.2 CBC record (RFC 5246 section 6.2.3.2): <c>padding_length + 1</c> bytes,
/// each holding <c>padding_length</c>, filling the content out to a whole number of blocks.
/// </summary>
internal static class TlsCbcPadding
{
    /// <summary>The most bytes padding can take: 255 padding bytes and the length byte.</summary>
    private const int MaxPaddingLength = 256;

    /// <summary>Appends the least padding that makes <paramref name="content"/> a whole number of blocks.</summary>
    /// <param name="content">The bytes to pad.</param>
    /// <param name="blockLength">The cipher's block length.</param>
    /// <returns>The content followed by its padding.</returns>
    public static byte[] Pad(ReadOnlySpan<byte> content, int blockLength)
    {
        var paddingLength = blockLength - 1 - (content.Length % blockLength);
        var padded = new byte[content.Length + paddingLength + 1];
        content.CopyTo(padded);
        padded.AsSpan(content.Length).Fill((byte)paddingLength);
        return padded;
    }

    /// <summary>
    /// Finds where the content ends in a decrypted record, checking the padding without a branch
    /// that depends on the padding's bytes (RFC 5246 section 6.2.3.2's note on timing). When the
    /// padding is malformed, the content is taken to end as if the padding were one byte long, so the
    /// caller still computes a MAC over nearly the whole record before refusing it.
    /// </summary>
    /// <param name="padded">The decrypted record.</param>
    /// <param name="trailerLength">How many bytes sit between the content and the padding: the MAC's
    /// length for MAC-then-encrypt, zero for encrypt-then-MAC. <paramref name="padded"/> is at least
    /// <c>trailerLength + 1</c> bytes long.</param>
    /// <param name="paddingIsValid">Whether the padding fits and every one of its bytes is right.</param>
    /// <returns>The content's length.</returns>
    public static int MeasureContent(ReadOnlySpan<byte> padded, int trailerLength, out bool paddingIsValid)
    {
        int paddingLength = padded[^1];
        var tooLong = (padded.Length - trailerLength - paddingLength - 1) >> 31;
        var mismatch = tooLong;
        var checkedCount = Math.Min(MaxPaddingLength, padded.Length);
        for (var index = 0; index < checkedCount; index++)
        {
            var insidePadding = (index - paddingLength - 1) >> 31;
            mismatch |= (padded[padded.Length - 1 - index] ^ paddingLength) & insidePadding;
        }

        paddingIsValid = mismatch == 0;
        return padded.Length - trailerLength - 1 - (paddingLength & ~tooLong);
    }
}
