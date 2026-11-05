namespace Surl.Hpack;

/// <summary>
/// The static Huffman code of RFC 7541 Appendix B, which HPACK (RFC 7541 section 5.2) and QPACK
/// (RFC 9204 section 4.1.2) use for string literals.
/// </summary>
/// <remarks>
/// The code is canonical: codes of one length are consecutive and run in symbol order, and each
/// length's first code follows on from the last code of the length before. So only the code lengths
/// are tabulated here and the codes are derived from them, exactly as Appendix B lists them, from
/// symbol 0 (<c>0x1ff8</c>, 13 bits) to EOS (<c>0x3fffffff</c>, 30 bits).
/// </remarks>
public static class HpackHuffman
{
    /// <summary>The symbol that marks the end of a string; it never appears inside one.</summary>
    public const int EndOfStringSymbol = 256;

    private const int LongestCodeLength = 30;

    private const int LongestPaddingBits = 7;

    private const int NoSymbol = -1;

    /// <summary>Code lengths in bits of symbols 0 to 255 and EOS (256), from RFC 7541 Appendix B.</summary>
    private static readonly byte[] CodeLengths =
    [
        13, 23, 28, 28, 28, 28, 28, 28, 28, 24, 30, 28, 28, 30, 28, 28, // 0-15
        28, 28, 28, 28, 28, 28, 30, 28, 28, 28, 28, 28, 28, 28, 28, 28, // 16-31
        6, 10, 10, 12, 13, 6, 8, 11, 10, 10, 8, 11, 8, 6, 6, 6, // ' ' to '/'
        5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 7, 8, 15, 6, 12, 10, // '0' to '?'
        13, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, // '@' to 'O'
        7, 7, 7, 7, 7, 7, 7, 7, 8, 7, 8, 13, 19, 13, 14, 6, // 'P' to '_'
        15, 5, 6, 5, 6, 5, 6, 6, 6, 5, 7, 7, 6, 6, 6, 5, // '`' to 'o'
        6, 7, 6, 5, 5, 6, 7, 7, 7, 7, 7, 15, 11, 14, 13, 28, // 'p' to 127
        20, 22, 20, 20, 22, 22, 22, 23, 22, 23, 23, 23, 23, 23, 24, 23, // 128-143
        24, 24, 22, 23, 24, 23, 23, 23, 23, 21, 22, 23, 22, 23, 23, 24, // 144-159
        22, 21, 20, 22, 22, 23, 23, 21, 23, 22, 22, 24, 21, 22, 23, 23, // 160-175
        21, 21, 22, 21, 23, 22, 23, 23, 20, 22, 22, 22, 23, 22, 22, 23, // 176-191
        26, 26, 20, 19, 22, 23, 22, 25, 26, 26, 26, 27, 27, 26, 24, 25, // 192-207
        19, 21, 26, 27, 27, 26, 27, 24, 21, 21, 26, 26, 28, 27, 27, 27, // 208-223
        20, 24, 20, 21, 22, 21, 21, 23, 22, 22, 25, 25, 24, 24, 26, 23, // 224-239
        26, 27, 26, 26, 27, 27, 27, 27, 27, 28, 27, 27, 27, 27, 27, 26, // 240-255
        30, // EOS
    ];

    /// <summary>The code of each symbol, right-aligned, derived from <see cref="CodeLengths" />.</summary>
    private static readonly uint[] Codes = new uint[CodeLengths.Length];

    /// <summary>For each code length, the first (smallest) code of that length.</summary>
    private static readonly uint[] FirstCodeOfLength = new uint[LongestCodeLength + 1];

    /// <summary>For each code length, how many symbols have a code of that length.</summary>
    private static readonly int[] SymbolCountOfLength = new int[LongestCodeLength + 1];

    /// <summary>For each code length, where its symbols start in <see cref="SymbolsByCode" />.</summary>
    private static readonly int[] FirstSymbolPositionOfLength = new int[LongestCodeLength + 1];

    /// <summary>Every symbol, ordered by code length and then by symbol value: canonical code order.</summary>
    private static readonly int[] SymbolsByCode = new int[CodeLengths.Length];

    static HpackHuffman()
    {
        foreach (var length in CodeLengths)
        {
            SymbolCountOfLength[length]++;
        }

        uint nextCode = 0;
        var position = 0;
        for (var length = 1; length <= LongestCodeLength; length++)
        {
            FirstCodeOfLength[length] = nextCode;
            FirstSymbolPositionOfLength[length] = position;
            nextCode = (nextCode + (uint)SymbolCountOfLength[length]) << 1;
            position += SymbolCountOfLength[length];
        }

        var filled = new int[LongestCodeLength + 1];
        for (var symbol = 0; symbol < CodeLengths.Length; symbol++)
        {
            var length = CodeLengths[symbol];
            SymbolsByCode[FirstSymbolPositionOfLength[length] + filled[length]] = symbol;
            Codes[symbol] = FirstCodeOfLength[length] + (uint)filled[length];
            filled[length]++;
        }
    }

    /// <summary>Gets how many bytes <paramref name="source" /> takes once Huffman-coded, padding included.</summary>
    /// <param name="source">The bytes to measure.</param>
    /// <returns>The encoded length in bytes.</returns>
    public static int GetEncodedLength(ReadOnlySpan<byte> source)
    {
        long bits = 0;
        foreach (var symbol in source)
        {
            bits += CodeLengths[symbol];
        }

        return (int)((bits + 7) / 8);
    }

    /// <summary>
    /// Huffman-codes <paramref name="source" />, padding the last byte with the most significant bits
    /// of the EOS code (all ones), as RFC 7541 section 5.2 requires.
    /// </summary>
    /// <param name="source">The bytes to encode.</param>
    /// <returns>The encoded bytes, <see cref="GetEncodedLength" /> of them.</returns>
    public static byte[] Encode(ReadOnlySpan<byte> source)
    {
        var encoded = new byte[GetEncodedLength(source)];
        ulong pending = 0;
        var pendingBits = 0;
        var written = 0;
        foreach (var symbol in source)
        {
            pending = (pending << CodeLengths[symbol]) | Codes[symbol];
            pendingBits += CodeLengths[symbol];
            while (pendingBits >= 8)
            {
                pendingBits -= 8;
                encoded[written++] = (byte)(pending >> pendingBits);
            }
        }

        if (pendingBits > 0)
        {
            encoded[written] = (byte)((pending << (8 - pendingBits)) | (0xFFu >> pendingBits));
        }

        return encoded;
    }

    /// <summary>Decodes a Huffman-coded string.</summary>
    /// <param name="source">The Huffman-coded bytes.</param>
    /// <param name="decoded">The decoded bytes, or empty when the string is malformed.</param>
    /// <param name="error">
    /// When the string is malformed, why: <see cref="HpackDecodingError.HuffmanEndOfStringSymbol" />,
    /// <see cref="HpackDecodingError.HuffmanPaddingLongerThanSevenBits" /> or
    /// <see cref="HpackDecodingError.HuffmanPaddingNotAllOnes" />.
    /// </param>
    /// <returns>Whether the string decoded.</returns>
    public static bool TryDecode(ReadOnlySpan<byte> source, out byte[] decoded, out HpackDecodingError error)
    {
        var output = new List<byte>(source.Length * 8 / 5);
        uint code = 0;
        var codeLength = 0;
        decoded = [];
        foreach (var octet in source)
        {
            for (var bit = 7; bit >= 0; bit--)
            {
                code = (code << 1) | (uint)((octet >> bit) & 1);
                codeLength++;
                var symbol = MatchSymbol(code, codeLength);
                if (symbol == EndOfStringSymbol)
                {
                    error = HpackDecodingError.HuffmanEndOfStringSymbol;
                    return false;
                }

                if (symbol != NoSymbol)
                {
                    output.Add((byte)symbol);
                    code = 0;
                    codeLength = 0;
                }
            }
        }

        return TryAcceptPadding(code, codeLength, output, out decoded, out error);
    }

    /// <summary>Gets the code RFC 7541 Appendix B gives a symbol, right-aligned, and its length.</summary>
    /// <param name="symbol">A byte value, or <see cref="EndOfStringSymbol" />.</param>
    /// <returns>The code and its length in bits.</returns>
    public static (uint Code, int Length) GetCode(int symbol) => (Codes[symbol], CodeLengths[symbol]);

    private static int MatchSymbol(uint code, int codeLength)
    {
        var offset = code - FirstCodeOfLength[codeLength];
        return offset < SymbolCountOfLength[codeLength]
            ? SymbolsByCode[FirstSymbolPositionOfLength[codeLength] + (int)offset]
            : NoSymbol;
    }

    private static bool TryAcceptPadding(
        uint code, int codeLength, List<byte> output, out byte[] decoded, out HpackDecodingError error)
    {
        decoded = [];
        error = codeLength > LongestPaddingBits
            ? HpackDecodingError.HuffmanPaddingLongerThanSevenBits
            : HpackDecodingError.HuffmanPaddingNotAllOnes;
        if (codeLength > LongestPaddingBits || code != (1u << codeLength) - 1)
        {
            return false;
        }

        decoded = [.. output];
        return true;
    }
}
