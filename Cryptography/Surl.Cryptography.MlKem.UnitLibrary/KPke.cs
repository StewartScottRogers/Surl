namespace Surl.Cryptography.MlKem;

/// <summary>
/// K-PKE, the public-key encryption scheme under ML-KEM (FIPS 203 section 5), for one parameter
/// set: the rank k and the compression widths d_u and d_v. eta_1 = eta_2 = 2 in both parameter
/// sets this library builds, ML-KEM-768 and ML-KEM-1024 (FIPS 203 section 8, Table 2).
/// </summary>
internal sealed class KPke
{
    /// <summary>ML-KEM-768's K-PKE: k = 3, d_u = 10, d_v = 4.</summary>
    public static readonly KPke ForMlKem768 = new(rank: 3, compressedUBits: 10, compressedVBits: 4);

    /// <summary>ML-KEM-1024's K-PKE: k = 4, d_u = 11, d_v = 5.</summary>
    public static readonly KPke ForMlKem1024 = new(rank: 4, compressedUBits: 11, compressedVBits: 5);

    private readonly int compressedUBits;
    private readonly int compressedVBits;
    private readonly int uBytes;

    private KPke(int rank, int compressedUBits, int compressedVBits)
    {
        Rank = rank;
        this.compressedUBits = compressedUBits;
        this.compressedVBits = compressedVBits;
        uBytes = 32 * compressedUBits;
        DecryptionKeySize = MlKemPolynomial.EncodedSize * rank;
        EncryptionKeySize = DecryptionKeySize + 32;
        CiphertextSize = 32 * ((compressedUBits * rank) + compressedVBits);
    }

    /// <summary>k, the rank of the module.</summary>
    public int Rank { get; }

    /// <summary>The bytes of K-PKE's encryption key: 384k + 32.</summary>
    public int EncryptionKeySize { get; }

    /// <summary>The bytes of K-PKE's decryption key: 384k.</summary>
    public int DecryptionKeySize { get; }

    /// <summary>The bytes of a ciphertext: 32(d_u k + d_v).</summary>
    public int CiphertextSize { get; }

    /// <summary>K-PKE.KeyGen (FIPS 203 Algorithm 13) from the 32-byte seed <paramref name="d"/>.</summary>
    public void GenerateKey(ReadOnlySpan<byte> d, Span<byte> encryptionKey, Span<byte> decryptionKey)
    {
        var seeds = Sha3.Sha3_512([.. d, (byte)Rank]);
        var rho = seeds.AsSpan(0, 32);
        var sigma = seeds.AsSpan(32, 32);
        var matrix = SampleMatrix(rho);
        var secret = SampleVector(sigma, 0);
        var error = SampleVector(sigma, Rank);
        for (var i = 0; i < Rank; i++)
        {
            MlKemPolynomial.Ntt(secret[i]);
            MlKemPolynomial.Ntt(error[i]);
        }

        for (var i = 0; i < Rank; i++)
        {
            var t = error[i];
            for (var j = 0; j < Rank; j++)
            {
                MlKemPolynomial.MultiplyNttsAndAdd(matrix[i][j], secret[j], t);
            }

            MlKemPolynomial.Encode(t, 12, encryptionKey.Slice(i * MlKemPolynomial.EncodedSize, MlKemPolynomial.EncodedSize));
            MlKemPolynomial.Encode(secret[i], 12, decryptionKey.Slice(i * MlKemPolynomial.EncodedSize, MlKemPolynomial.EncodedSize));
        }

        rho.CopyTo(encryptionKey[DecryptionKeySize..]);
    }

    /// <summary>
    /// K-PKE.Encrypt (FIPS 203 Algorithm 14): encrypts the 32-byte message <paramref name="m"/>
    /// with the randomness <paramref name="r"/> into <paramref name="ciphertext"/>.
    /// </summary>
    public void Encrypt(ReadOnlySpan<byte> encryptionKey, ReadOnlySpan<byte> m, ReadOnlySpan<byte> r, Span<byte> ciphertext)
    {
        var t = DecodeVector(encryptionKey, 12);
        var matrix = SampleMatrix(encryptionKey[DecryptionKeySize..]);
        var y = SampleVector(r, 0);
        var e1 = SampleVector(r, Rank);
        var e2 = MlKemPolynomial.SampleCenteredBinomial(r, (byte)(2 * Rank));
        foreach (var polynomial in y)
        {
            MlKemPolynomial.Ntt(polynomial);
        }

        for (var i = 0; i < Rank; i++)
        {
            var u = new int[MlKemPolynomial.Degree];
            for (var j = 0; j < Rank; j++)
            {
                MlKemPolynomial.MultiplyNttsAndAdd(matrix[j][i], y[j], u);
            }

            MlKemPolynomial.InverseNtt(u);
            MlKemPolynomial.Add(u, e1[i]);
            MlKemPolynomial.Compress(u, compressedUBits);
            MlKemPolynomial.Encode(u, compressedUBits, ciphertext.Slice(i * uBytes, uBytes));
        }

        var v = InnerProduct(t, y);
        MlKemPolynomial.Add(v, e2);
        var mu = MlKemPolynomial.Decode(m, 1);
        MlKemPolynomial.Decompress(mu, 1);
        MlKemPolynomial.Add(v, mu);
        MlKemPolynomial.Compress(v, compressedVBits);
        MlKemPolynomial.Encode(v, compressedVBits, ciphertext[(Rank * uBytes)..]);
    }

    /// <summary>K-PKE.Decrypt (FIPS 203 Algorithm 15): the 32-byte message <paramref name="ciphertext"/> carries.</summary>
    public void Decrypt(ReadOnlySpan<byte> decryptionKey, ReadOnlySpan<byte> ciphertext, Span<byte> m)
    {
        var u = DecodeVector(ciphertext, compressedUBits);
        foreach (var polynomial in u)
        {
            MlKemPolynomial.Decompress(polynomial, compressedUBits);
            MlKemPolynomial.Ntt(polynomial);
        }

        var v = MlKemPolynomial.Decode(ciphertext[(Rank * uBytes)..], compressedVBits);
        MlKemPolynomial.Decompress(v, compressedVBits);
        var product = InnerProduct(DecodeVector(decryptionKey, 12), u);
        MlKemPolynomial.Subtract(v, product);
        MlKemPolynomial.Compress(v, 1);
        MlKemPolynomial.Encode(v, 1, m);
    }

    /// <summary>
    /// Whether every ByteDecode_12 value of the encryption key's t is below q: FIPS 203 section
    /// 7.2's modulus check, the same as ByteEncode_12(ByteDecode_12(t)) == t.
    /// </summary>
    public bool HasReducedCoefficients(ReadOnlySpan<byte> encryptionKey)
    {
        var below = true;
        foreach (var polynomial in DecodeVector(encryptionKey, 12))
        {
            below &= !polynomial.AsSpan().ContainsAnyInRange(MlKemPolynomial.Modulus, 4095);
        }

        return below;
    }

    /// <summary>NTT^-1 of the dot product of two vectors in the NTT domain.</summary>
    private int[] InnerProduct(int[][] left, int[][] right)
    {
        var sum = new int[MlKemPolynomial.Degree];
        for (var i = 0; i < Rank; i++)
        {
            MlKemPolynomial.MultiplyNttsAndAdd(left[i], right[i], sum);
        }

        MlKemPolynomial.InverseNtt(sum);
        return sum;
    }

    /// <summary>A_hat, with A_hat[i][j] = SampleNTT(rho || j || i) (FIPS 203 Algorithm 13 step 6).</summary>
    private int[][][] SampleMatrix(ReadOnlySpan<byte> rho)
    {
        var matrix = new int[Rank][][];
        for (var i = 0; i < Rank; i++)
        {
            matrix[i] = new int[Rank][];
            for (var j = 0; j < Rank; j++)
            {
                matrix[i][j] = MlKemPolynomial.SampleNtt(rho, (byte)j, (byte)i);
            }
        }

        return matrix;
    }

    /// <summary>k polynomials from SamplePolyCBD_2 with the nonces <paramref name="firstNonce"/> onwards.</summary>
    private int[][] SampleVector(ReadOnlySpan<byte> seed, int firstNonce)
    {
        var vector = new int[Rank][];
        for (var i = 0; i < Rank; i++)
        {
            vector[i] = MlKemPolynomial.SampleCenteredBinomial(seed, (byte)(firstNonce + i));
        }

        return vector;
    }

    private int[][] DecodeVector(ReadOnlySpan<byte> input, int bits)
    {
        var vector = new int[Rank][];
        for (var i = 0; i < Rank; i++)
        {
            vector[i] = MlKemPolynomial.Decode(input.Slice(i * 32 * bits, 32 * bits), bits);
        }

        return vector;
    }
}
