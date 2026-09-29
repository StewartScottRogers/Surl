using System.Buffers.Binary;

namespace Surl.Cryptography;

/// <summary>
/// Computes SHA-512/256 (FIPS 180-4, sections 5.3.6.2 and 6.7), the hash Digest's
/// <c>SHA-512-256</c> algorithm (RFC 7616) names, which the base class library does not provide.
/// </summary>
/// <remarks>
/// SHA-512/256 is SHA-512 started from its own initial hash value and truncated to the
/// first 256 bits, so it is not SHA-512 truncated. Adapted from the Curl port's
/// <c>Sha512Slash256</c>; code is not a verdict (ADR-0003), and every expected hash in the
/// tests comes from NIST.
/// </remarks>
public static class Sha512Slash256
{
    /// <summary>The length of a SHA-512/256 hash, in bytes.</summary>
    public const int HashLength = 32;

    private const int BlockLength = 128;

    // The length field is 128 bits; an array's length only ever fills the low 64.
    private const int LengthFieldLength = 16;

    private static readonly ulong[] InitialHashValue =
    [
        0x22312194FC2BF72C, 0x9F555FA3C84C64C2, 0x2393B86B6F53B151, 0x963877195940EABD,
        0x96283EE2A88EFFE3, 0xBE5E1E2553863992, 0x2B0199FC2C85B8AA, 0x0EB72DDC81C52CA2,
    ];

    private static readonly ulong[] RoundConstants =
    [
        0x428A2F98D728AE22, 0x7137449123EF65CD, 0xB5C0FBCFEC4D3B2F, 0xE9B5DBA58189DBBC,
        0x3956C25BF348B538, 0x59F111F1B605D019, 0x923F82A4AF194F9B, 0xAB1C5ED5DA6D8118,
        0xD807AA98A3030242, 0x12835B0145706FBE, 0x243185BE4EE4B28C, 0x550C7DC3D5FFB4E2,
        0x72BE5D74F27B896F, 0x80DEB1FE3B1696B1, 0x9BDC06A725C71235, 0xC19BF174CF692694,
        0xE49B69C19EF14AD2, 0xEFBE4786384F25E3, 0x0FC19DC68B8CD5B5, 0x240CA1CC77AC9C65,
        0x2DE92C6F592B0275, 0x4A7484AA6EA6E483, 0x5CB0A9DCBD41FBD4, 0x76F988DA831153B5,
        0x983E5152EE66DFAB, 0xA831C66D2DB43210, 0xB00327C898FB213F, 0xBF597FC7BEEF0EE4,
        0xC6E00BF33DA88FC2, 0xD5A79147930AA725, 0x06CA6351E003826F, 0x142929670A0E6E70,
        0x27B70A8546D22FFC, 0x2E1B21385C26C926, 0x4D2C6DFC5AC42AED, 0x53380D139D95B3DF,
        0x650A73548BAF63DE, 0x766A0ABB3C77B2A8, 0x81C2C92E47EDAEE6, 0x92722C851482353B,
        0xA2BFE8A14CF10364, 0xA81A664BBC423001, 0xC24B8B70D0F89791, 0xC76C51A30654BE30,
        0xD192E819D6EF5218, 0xD69906245565A910, 0xF40E35855771202A, 0x106AA07032BBD1B8,
        0x19A4C116B8D2D0C8, 0x1E376C085141AB53, 0x2748774CDF8EEB99, 0x34B0BCB5E19B48A8,
        0x391C0CB3C5C95A63, 0x4ED8AA4AE3418ACB, 0x5B9CCA4F7763E373, 0x682E6FF3D6B2B8A3,
        0x748F82EE5DEFB2FC, 0x78A5636F43172F60, 0x84C87814A1F0AB72, 0x8CC702081A6439EC,
        0x90BEFFFA23631E28, 0xA4506CEBDE82BDE9, 0xBEF9A3F7B2C67915, 0xC67178F2E372532B,
        0xCA273ECEEA26619C, 0xD186B8C721C0C207, 0xEADA7DD6CDE0EB1E, 0xF57D4F7FEE6ED178,
        0x06F067AA72176FBA, 0x0A637DC5A2C898A6, 0x113F9804BEF90DAE, 0x1B710B35131C471B,
        0x28DB77F523047D84, 0x32CAAB7B40C72493, 0x3C9EBE0A15C9BEBC, 0x431D67C49C100D4C,
        0x4CC5D4BECB3E42B6, 0x597F299CFC657E2A, 0x5FCB6FAB3AD6FAEC, 0x6C44198C4A475817,
    ];

    /// <summary>
    /// Computes the SHA-512/256 hash of <paramref name="message" />.
    /// </summary>
    /// <param name="message">The bytes to hash.</param>
    /// <returns>The 32-byte hash.</returns>
    public static byte[] HashData(ReadOnlySpan<byte> message)
    {
        Span<ulong> state = stackalloc ulong[8];
        InitialHashValue.CopyTo(state);

        int wholeBlocksLength = message.Length - (message.Length % BlockLength);
        for (int offset = 0; offset < wholeBlocksLength; offset += BlockLength)
        {
            Compress(state, message.Slice(offset, BlockLength));
        }

        Span<byte> tail = stackalloc byte[2 * BlockLength];
        int tailLength = PadTail(message[wholeBlocksLength..], (ulong)message.Length, tail);
        for (int offset = 0; offset < tailLength; offset += BlockLength)
        {
            Compress(state, tail.Slice(offset, BlockLength));
        }

        byte[] hash = new byte[HashLength];
        for (int word = 0; word < HashLength / 8; word++)
        {
            BinaryPrimitives.WriteUInt64BigEndian(hash.AsSpan(word * 8), state[word]);
        }

        return hash;
    }

    // Writes the last partial block, a 0x80 byte, zeros to 112 mod 128 and the message
    // length in bits as a 128-bit big-endian number into tail, and returns the bytes used:
    // one block, or two when the partial block leaves less than 17 bytes free.
    private static int PadTail(ReadOnlySpan<byte> partialBlock, ulong messageLength, Span<byte> tail)
    {
        tail.Clear();
        partialBlock.CopyTo(tail);
        tail[partialBlock.Length] = 0x80;
        int tailLength = partialBlock.Length + 1 + LengthFieldLength <= BlockLength ? BlockLength : 2 * BlockLength;
        BinaryPrimitives.WriteUInt64BigEndian(tail[(tailLength - 8)..], messageLength * 8);
        return tailLength;
    }

    private static void Compress(Span<ulong> state, ReadOnlySpan<byte> block)
    {
        Span<ulong> schedule = stackalloc ulong[80];
        for (int t = 0; t < 16; t++)
        {
            schedule[t] = BinaryPrimitives.ReadUInt64BigEndian(block[(t * 8)..]);
        }

        for (int t = 16; t < 80; t++)
        {
            schedule[t] = SmallSigma1(schedule[t - 2]) + schedule[t - 7] + SmallSigma0(schedule[t - 15]) + schedule[t - 16];
        }

        Span<ulong> working = stackalloc ulong[8];
        state.CopyTo(working);
        for (int t = 0; t < 80; t++)
        {
            Round(working, RoundConstants[t] + schedule[t]);
        }

        for (int i = 0; i < 8; i++)
        {
            state[i] += working[i];
        }
    }

    // One SHA-512 round over a..h, held in working[0..7].
    private static void Round(Span<ulong> working, ulong constantPlusWord)
    {
        ulong a = working[0], b = working[1], c = working[2], e = working[4], f = working[5], g = working[6];
        ulong temporary1 = working[7] + BigSigma1(e) + ((e & f) ^ (~e & g)) + constantPlusWord;
        ulong temporary2 = BigSigma0(a) + ((a & b) ^ (a & c) ^ (b & c));
        working[7] = g;
        working[6] = f;
        working[5] = e;
        working[4] = working[3] + temporary1;
        working[3] = c;
        working[2] = b;
        working[1] = a;
        working[0] = temporary1 + temporary2;
    }

    private static ulong BigSigma0(ulong x) => ulong.RotateRight(x, 28) ^ ulong.RotateRight(x, 34) ^ ulong.RotateRight(x, 39);

    private static ulong BigSigma1(ulong x) => ulong.RotateRight(x, 14) ^ ulong.RotateRight(x, 18) ^ ulong.RotateRight(x, 41);

    private static ulong SmallSigma0(ulong x) => ulong.RotateRight(x, 1) ^ ulong.RotateRight(x, 8) ^ (x >> 7);

    private static ulong SmallSigma1(ulong x) => ulong.RotateRight(x, 19) ^ ulong.RotateRight(x, 61) ^ (x >> 6);
}
