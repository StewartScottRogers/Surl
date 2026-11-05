namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The part of a representation one <c>2.05</c> carries (RFC 7959 sections 2.3, 2.4 and 4;
/// ADR-0097 decision 4): the whole of it when it fits one block and the client asked for no block,
/// else the block the request's Block2 names, with Block2 and, on block 0 or when asked, Size2.
/// </summary>
/// <param name="Offset">The first byte carried.</param>
/// <param name="Count">How many bytes are carried.</param>
/// <param name="Options">Block2 and Size2, as the window needs them.</param>
internal sealed record CoapBlock2Window(long Offset, int Count, IReadOnlyList<CoapOption> Options)
{
    /// <summary>
    /// Selects the window of a representation of <paramref name="length"/> bytes that answers a
    /// request with <paramref name="options"/>.
    /// </summary>
    /// <remarks>
    /// The request's Block2 is control usage: its M is ignored, its SZX is used when smaller than
    /// <paramref name="preferredSizeExponent"/>, and its NUM selects the bytes at NUM times its
    /// own block size, renumbered in the size used. A block that starts past the end names no
    /// part of the representation.
    /// </remarks>
    /// <param name="options">The request's options.</param>
    /// <param name="length">The representation's length.</param>
    /// <param name="preferredSizeExponent">The server's largest SZX.</param>
    /// <returns>The window, or null when the requested block starts past the end.</returns>
    public static CoapBlock2Window? Select(CoapRequestOptions options, long length, int preferredSizeExponent)
    {
        if (options.Block2 is { } asked)
        {
            return SelectBlock(asked, options.AsksForSize2, length, preferredSizeExponent);
        }

        if (length > CoapBlockOption.SizeOf(preferredSizeExponent))
        {
            return SelectBlock(new CoapBlockOption(0, false, preferredSizeExponent), options.AsksForSize2, length, preferredSizeExponent);
        }

        return new CoapBlock2Window(0, (int)length, options.AsksForSize2 ? [Size2(length)] : []);
    }

    private static CoapBlock2Window? SelectBlock(CoapBlockOption asked, bool asksForSize2, long length, int preferredSizeExponent)
    {
        var offset = asked.Offset;

        if (offset > 0 && offset >= length)
        {
            return null;
        }

        var sizeExponent = Math.Min(asked.SizeExponent, preferredSizeExponent);
        var count = (int)Math.Min(CoapBlockOption.SizeOf(sizeExponent), length - offset);
        var block = CoapBlockOption.At(offset, offset + count < length, sizeExponent).ToOption(CoapOptionNumber.Block2);

        return new CoapBlock2Window(offset, count, offset == 0 || asksForSize2 ? [block, Size2(length)] : [block]);
    }

    private static CoapOption Size2(long length) =>
        CoapOption.FromUInt(CoapOptionNumber.Size2, (uint)Math.Min(length, uint.MaxValue));
}
