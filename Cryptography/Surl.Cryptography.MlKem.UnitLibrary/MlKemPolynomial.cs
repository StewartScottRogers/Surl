namespace Surl.Cryptography.MlKem;

/// <summary>
/// The arithmetic on polynomials of Z_q[X]/(X^256 + 1), q = 3329, that ML-KEM is built from
/// (FIPS 203 sections 4.2 and 4.3): byte encoding and decoding, compression, the sampling
/// algorithms, the number-theoretic transform and multiplication in its domain. A polynomial is
/// 256 coefficients, each kept in [0, q) unless a member says otherwise.
/// </summary>
internal static class MlKemPolynomial
{
    /// <summary>The number of coefficients, n.</summary>
    public const int Degree = 256;

    /// <summary>The modulus, q.</summary>
    public const int Modulus = 3329;

    /// <summary>The bytes ByteEncode_12 writes for one polynomial.</summary>
    public const int EncodedSize = 384;

    /// <summary>128^-1 mod q, the scale NTT^-1 ends with (FIPS 203 Algorithm 10).</summary>
    private const int InverseOf128 = 3303;

    /// <summary>zeta^BitRev7(i) mod q for i in 0..127, with zeta = 17 (FIPS 203 section 4.3, Appendix A).</summary>
    private static readonly int[] Zetas = new int[128];

    /// <summary>zeta^(2 BitRev7(i) + 1) mod q for i in 0..127, BaseCaseMultiply's gamma (FIPS 203 Algorithm 11).</summary>
    private static readonly int[] Gammas = new int[128];

    static MlKemPolynomial()
    {
        for (var i = 0; i < 128; i++)
        {
            var exponent = BitReverse7(i);
            Zetas[i] = Power(17, exponent);
            Gammas[i] = Power(17, (2 * exponent) + 1);
        }
    }

    /// <summary>ByteEncode_d (FIPS 203 Algorithm 5): each coefficient's low <paramref name="bits"/> bits, least significant first.</summary>
    public static void Encode(ReadOnlySpan<int> polynomial, int bits, Span<byte> output)
    {
        output[..(32 * bits)].Clear();
        var position = 0;
        foreach (var coefficient in polynomial)
        {
            for (var bit = 0; bit < bits; bit++, position++)
            {
                output[position >> 3] |= (byte)(((coefficient >> bit) & 1) << (position & 7));
            }
        }
    }

    /// <summary>
    /// ByteDecode_d (FIPS 203 Algorithm 6) without the final reduction: each coefficient is the
    /// next <paramref name="bits"/> bits. For d = 12 a value may reach 4095, which
    /// <see cref="MultiplyNttsAndAdd"/> accepts and the encapsulation key check rejects.
    /// </summary>
    public static int[] Decode(ReadOnlySpan<byte> input, int bits)
    {
        var polynomial = new int[Degree];
        var position = 0;
        for (var index = 0; index < Degree; index++)
        {
            for (var bit = 0; bit < bits; bit++, position++)
            {
                polynomial[index] |= ((input[position >> 3] >> (position & 7)) & 1) << bit;
            }
        }

        return polynomial;
    }

    /// <summary>Compress_d (FIPS 203 section 4.2.1) of every coefficient: round(2^d / q * x) mod 2^d.</summary>
    public static void Compress(Span<int> polynomial, int bits)
    {
        for (var index = 0; index < Degree; index++)
        {
            polynomial[index] = (((polynomial[index] << (bits + 1)) + Modulus) / (2 * Modulus)) & ((1 << bits) - 1);
        }
    }

    /// <summary>Decompress_d (FIPS 203 section 4.2.1) of every coefficient: round(q / 2^d * y).</summary>
    public static void Decompress(Span<int> polynomial, int bits)
    {
        for (var index = 0; index < Degree; index++)
        {
            polynomial[index] = ((polynomial[index] * Modulus) + (1 << (bits - 1))) >> bits;
        }
    }

    /// <summary>
    /// SampleNTT (FIPS 203 Algorithm 7): a polynomial in the NTT domain drawn by rejection from
    /// SHAKE128(<paramref name="seed"/> || <paramref name="first"/> || <paramref name="second"/>).
    /// </summary>
    public static int[] SampleNtt(ReadOnlySpan<byte> seed, byte first, byte second)
    {
        var xof = Sha3.Shake128([.. seed, first, second]);
        var polynomial = new int[Degree];
        Span<byte> bytes = stackalloc byte[3];
        var count = 0;
        while (count < Degree)
        {
            xof.Squeeze(bytes);
            var low = bytes[0] | ((bytes[1] & 0x0F) << 8);
            var high = (bytes[1] >> 4) | (bytes[2] << 4);
            count = Accept(polynomial, count, low);
            count = Accept(polynomial, count, high);
        }

        return polynomial;
    }

    /// <summary>
    /// SamplePolyCBD_2 (FIPS 203 Algorithm 8) of PRF_2(<paramref name="seed"/>, <paramref name="nonce"/>)
    /// = SHAKE256(seed || nonce) to 128 bytes: each coefficient is the sum of two bits minus the
    /// sum of the next two. eta_1 and eta_2 are both 2 in ML-KEM-768 and ML-KEM-1024.
    /// </summary>
    public static int[] SampleCenteredBinomial(ReadOnlySpan<byte> seed, byte nonce)
    {
        var bytes = Sha3.Shake256([.. seed, nonce], 128);
        var polynomial = new int[Degree];
        for (var index = 0; index < Degree; index++)
        {
            var nibble = bytes[index >> 1] >> ((index & 1) * 4);
            var x = (nibble & 1) + ((nibble >> 1) & 1);
            var y = ((nibble >> 2) & 1) + ((nibble >> 3) & 1);
            polynomial[index] = (x - y + Modulus) % Modulus;
        }

        return polynomial;
    }

    /// <summary>NTT (FIPS 203 Algorithm 9), in place.</summary>
    public static void Ntt(Span<int> polynomial)
    {
        var zetaIndex = 1;
        for (var length = 128; length >= 2; length /= 2)
        {
            for (var start = 0; start < Degree; start += 2 * length)
            {
                var zeta = Zetas[zetaIndex++];
                for (var j = start; j < start + length; j++)
                {
                    var t = zeta * polynomial[j + length] % Modulus;
                    polynomial[j + length] = (polynomial[j] - t + Modulus) % Modulus;
                    polynomial[j] = (polynomial[j] + t) % Modulus;
                }
            }
        }
    }

    /// <summary>NTT^-1 (FIPS 203 Algorithm 10), in place.</summary>
    public static void InverseNtt(Span<int> polynomial)
    {
        var zetaIndex = 127;
        for (var length = 2; length <= 128; length *= 2)
        {
            for (var start = 0; start < Degree; start += 2 * length)
            {
                var zeta = Zetas[zetaIndex--];
                for (var j = start; j < start + length; j++)
                {
                    var t = polynomial[j];
                    polynomial[j] = (t + polynomial[j + length]) % Modulus;
                    polynomial[j + length] = zeta * ((polynomial[j + length] - t + Modulus) % Modulus) % Modulus;
                }
            }
        }

        for (var index = 0; index < Degree; index++)
        {
            polynomial[index] = polynomial[index] * InverseOf128 % Modulus;
        }
    }

    /// <summary>
    /// Adds MultiplyNTTs(<paramref name="f"/>, <paramref name="g"/>) (FIPS 203 Algorithms 11 and
    /// 12) to <paramref name="sum"/>. <paramref name="f"/> may hold unreduced 12-bit values.
    /// </summary>
    public static void MultiplyNttsAndAdd(ReadOnlySpan<int> f, ReadOnlySpan<int> g, Span<int> sum)
    {
        for (var i = 0; i < 128; i++)
        {
            int a0 = f[2 * i], a1 = f[(2 * i) + 1], b0 = g[2 * i], b1 = g[(2 * i) + 1];
            var c0 = ((a0 * b0) + (a1 * b1 % Modulus * Gammas[i])) % Modulus;
            var c1 = ((a0 * b1) + (a1 * b0)) % Modulus;
            sum[2 * i] = (sum[2 * i] + c0) % Modulus;
            sum[(2 * i) + 1] = (sum[(2 * i) + 1] + c1) % Modulus;
        }
    }

    /// <summary>Adds <paramref name="g"/> to <paramref name="f"/>, coefficient by coefficient.</summary>
    public static void Add(Span<int> f, ReadOnlySpan<int> g)
    {
        for (var index = 0; index < Degree; index++)
        {
            f[index] = (f[index] + g[index]) % Modulus;
        }
    }

    /// <summary>Subtracts <paramref name="g"/> from <paramref name="f"/>, coefficient by coefficient.</summary>
    public static void Subtract(Span<int> f, ReadOnlySpan<int> g)
    {
        for (var index = 0; index < Degree; index++)
        {
            f[index] = (f[index] - g[index] + Modulus) % Modulus;
        }
    }

    /// <summary>BitRev7 (FIPS 203 section 2.3): the 7-bit value with its bits in reverse order.</summary>
    public static int BitReverse7(int value)
    {
        var reversed = 0;
        for (var bit = 0; bit < 7; bit++)
        {
            reversed |= ((value >> bit) & 1) << (6 - bit);
        }

        return reversed;
    }

    /// <summary>Appends <paramref name="candidate"/> when it is below q and the polynomial is not yet full; returns the new count.</summary>
    private static int Accept(int[] polynomial, int count, int candidate)
    {
        if (candidate < Modulus && count < Degree)
        {
            polynomial[count] = candidate;
            return count + 1;
        }

        return count;
    }

    private static int Power(int value, int exponent)
    {
        var result = 1;
        for (var step = 0; step < exponent; step++)
        {
            result = result * value % Modulus;
        }

        return result;
    }
}
