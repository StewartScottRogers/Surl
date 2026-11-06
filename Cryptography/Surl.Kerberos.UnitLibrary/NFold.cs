namespace Surl.Kerberos;

/// <summary>
/// The <c>n-fold</c> function of RFC 3961 section 5.1, which stretches or shrinks a constant to
/// the cipher's block size for key derivation.
/// </summary>
internal static class NFold
{
    /// <summary>Folds <paramref name="input" /> to <paramref name="outputLength" /> bytes.</summary>
    /// <param name="input">The bytes to fold; at least one.</param>
    /// <param name="outputLength">The length of the result, in bytes; at least one.</param>
    /// <returns>The folded bytes.</returns>
    public static byte[] Fold(ReadOnlySpan<byte> input, int outputLength)
    {
        int inputLength = input.Length;
        int inputBits = inputLength * 8;
        int leastCommonMultiple = outputLength / GreatestCommonDivisor(outputLength, inputLength) * inputLength;
        byte[] output = new byte[outputLength];
        int carry = 0;

        // The output is the ones' complement sum of copies of the input, each rotated 13 bits
        // right of the one before, laid end to end over the least common multiple of the two
        // lengths; this walks that string from its last byte to its first, as MIT krb5 does.
        for (int index = leastCommonMultiple - 1; index >= 0; index--)
        {
            int mostSignificantBit = ((inputBits - 1)
                + ((inputBits + 13) * (index / inputLength))
                + ((inputLength - (index % inputLength)) * 8)) % inputBits;
            int high = input[(inputLength - 1 - (mostSignificantBit >> 3)) % inputLength];
            int low = input[(inputLength - (mostSignificantBit >> 3)) % inputLength];
            carry += (((high << 8) | low) >> ((mostSignificantBit & 7) + 1)) & 0xFF;
            carry += output[index % outputLength];
            output[index % outputLength] = (byte)carry;
            carry >>= 8;
        }

        // The end-around carry of the ones' complement sum; with no carry this adds zero.
        for (int index = outputLength - 1; index >= 0; index--)
        {
            carry += output[index];
            output[index] = (byte)carry;
            carry >>= 8;
        }

        return output;
    }

    private static int GreatestCommonDivisor(int first, int second)
    {
        while (second != 0)
        {
            (first, second) = (second, first % second);
        }

        return first;
    }
}
