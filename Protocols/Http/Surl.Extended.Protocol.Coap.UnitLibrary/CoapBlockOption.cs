namespace Surl.Extended.Protocol.Coap;

/// <summary>
/// The value of a Block1 or Block2 option (RFC 7959 section 2.2): the block number NUM, the more
/// flag M, and the size exponent SZX, which makes the block size 2 to the power SZX + 4.
/// </summary>
/// <param name="Number">NUM, the block's number, at most 20 bits.</param>
/// <param name="More">M: whether more blocks follow.</param>
/// <param name="SizeExponent">SZX, 0 to 7; 7 is reserved.</param>
internal readonly record struct CoapBlockOption(uint Number, bool More, int SizeExponent)
{
    /// <summary>SZX 6, 1024 bytes: the largest block size and the server's preference (ADR-0097 decision 4).</summary>
    public const int LargestSizeExponent = 6;

    /// <summary>SZX 7, which section 2.2 reserves: a request carrying it is <c>4.00</c>.</summary>
    public const int ReservedSizeExponent = 7;

    /// <summary>The block size: 16 to 1024 bytes.</summary>
    public int Size => SizeOf(SizeExponent);

    /// <summary>Where the block starts in the whole body: NUM times the block size.</summary>
    public long Offset => (long)Number << (SizeExponent + 4);

    /// <summary>The block size SZX <paramref name="sizeExponent"/> names.</summary>
    /// <param name="sizeExponent">SZX.</param>
    /// <returns>2 to the power <paramref name="sizeExponent"/> + 4.</returns>
    public static int SizeOf(int sizeExponent) => 1 << (sizeExponent + 4);

    /// <summary>
    /// The block of size SZX <paramref name="sizeExponent"/> that starts at <paramref name="offset"/>.
    /// </summary>
    /// <param name="offset">A multiple of the block size.</param>
    /// <param name="more">M.</param>
    /// <param name="sizeExponent">SZX.</param>
    /// <returns>The block.</returns>
    public static CoapBlockOption At(long offset, bool more, int sizeExponent) =>
        new((uint)(offset >> (sizeExponent + 4)), more, sizeExponent);

    /// <summary>Reads a Block1 or Block2 option's uint value (section 2.2).</summary>
    /// <param name="option">The option.</param>
    /// <returns>The block.</returns>
    public static CoapBlockOption Read(CoapOption option)
    {
        var value = option.ReadUInt();

        return new CoapBlockOption(value >> 4, (value & 0x08) != 0, (int)(value & 0x07));
    }

    /// <summary>Writes the block as option <paramref name="number"/>, Block1 or Block2.</summary>
    /// <param name="number">The option number.</param>
    /// <returns>The option, in the fewest bytes.</returns>
    public CoapOption ToOption(int number) =>
        CoapOption.FromUInt(number, (Number << 4) | (More ? 0x08u : 0u) | (uint)SizeExponent);

    /// <summary>The text RFC 7959 section 3 writes a block in: <c>NUM/M/size</c>, such as <c>0/1/128</c>.</summary>
    /// <returns>The block's text.</returns>
    public override string ToString() => $"{Number}/{(More ? 1 : 0)}/{Size}";
}
