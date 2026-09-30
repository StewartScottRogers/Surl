using System.Buffers.Binary;

namespace Surl.Cryptography;

/// <summary>
/// Encrypts one 8-byte block with DES (FIPS 46-3), for NTLMv1's <c>LMOWFv1</c> and <c>DESL</c>
/// ([MS-NLMP] section 6). The base class library's <c>System.Security.Cryptography.DES</c> refuses
/// the weak and semi-weak keys, and NTLMv1 needs them: an empty password's LM key is all zero
/// bits, the first weak key, and an LM or NT hash can put any seven bytes in a <c>DESL</c> key.
/// </summary>
/// <remarks>
/// DES is broken; use it only where NTLMv1 requires it. Only encryption is built, because NTLMv1
/// never decrypts. The low bit of each key byte is the parity bit, which DES ignores, and so does
/// this. Every expected block in the tests comes from a published source or from the base class
/// library's DES under a key it accepts.
/// </remarks>
public static class Des
{
    /// <summary>The length of a DES block, in bytes.</summary>
    public const int BlockLength = 8;

    /// <summary>The length of a DES key, parity bits included, in bytes.</summary>
    public const int KeyLength = 8;

    private const int RoundCount = 16;

    private const uint TwentyEightBits = 0x0FFFFFFF;

    // FIPS 46-3's tables, as 1-based bit positions counted from the most significant bit.
    private static readonly byte[] InitialPermutation =
    [
        58, 50, 42, 34, 26, 18, 10, 2, 60, 52, 44, 36, 28, 20, 12, 4,
        62, 54, 46, 38, 30, 22, 14, 6, 64, 56, 48, 40, 32, 24, 16, 8,
        57, 49, 41, 33, 25, 17, 9, 1, 59, 51, 43, 35, 27, 19, 11, 3,
        61, 53, 45, 37, 29, 21, 13, 5, 63, 55, 47, 39, 31, 23, 15, 7,
    ];

    private static readonly byte[] FinalPermutation =
    [
        40, 8, 48, 16, 56, 24, 64, 32, 39, 7, 47, 15, 55, 23, 63, 31,
        38, 6, 46, 14, 54, 22, 62, 30, 37, 5, 45, 13, 53, 21, 61, 29,
        36, 4, 44, 12, 52, 20, 60, 28, 35, 3, 43, 11, 51, 19, 59, 27,
        34, 2, 42, 10, 50, 18, 58, 26, 33, 1, 41, 9, 49, 17, 57, 25,
    ];

    private static readonly byte[] Expansion =
    [
        32, 1, 2, 3, 4, 5, 4, 5, 6, 7, 8, 9, 8, 9, 10, 11, 12, 13, 12, 13, 14, 15, 16, 17,
        16, 17, 18, 19, 20, 21, 20, 21, 22, 23, 24, 25, 24, 25, 26, 27, 28, 29, 28, 29, 30, 31, 32, 1,
    ];

    private static readonly byte[] Permutation =
    [
        16, 7, 20, 21, 29, 12, 28, 17, 1, 15, 23, 26, 5, 18, 31, 10,
        2, 8, 24, 14, 32, 27, 3, 9, 19, 13, 30, 6, 22, 11, 4, 25,
    ];

    private static readonly byte[] PermutedChoice1 =
    [
        57, 49, 41, 33, 25, 17, 9, 1, 58, 50, 42, 34, 26, 18,
        10, 2, 59, 51, 43, 35, 27, 19, 11, 3, 60, 52, 44, 36,
        63, 55, 47, 39, 31, 23, 15, 7, 62, 54, 46, 38, 30, 22,
        14, 6, 61, 53, 45, 37, 29, 21, 13, 5, 28, 20, 12, 4,
    ];

    private static readonly byte[] PermutedChoice2 =
    [
        14, 17, 11, 24, 1, 5, 3, 28, 15, 6, 21, 10, 23, 19, 12, 4,
        26, 8, 16, 7, 27, 20, 13, 2, 41, 52, 31, 37, 47, 55, 30, 40,
        51, 45, 33, 48, 44, 49, 39, 56, 34, 53, 46, 42, 50, 36, 29, 32,
    ];

    private static readonly int[] KeyShifts = [1, 1, 2, 2, 2, 2, 2, 2, 1, 2, 2, 2, 2, 2, 2, 1];

    // S1 to S8, each four rows of sixteen.
    private static readonly byte[][] SubstitutionBoxes =
    [
        [
            14, 4, 13, 1, 2, 15, 11, 8, 3, 10, 6, 12, 5, 9, 0, 7,
            0, 15, 7, 4, 14, 2, 13, 1, 10, 6, 12, 11, 9, 5, 3, 8,
            4, 1, 14, 8, 13, 6, 2, 11, 15, 12, 9, 7, 3, 10, 5, 0,
            15, 12, 8, 2, 4, 9, 1, 7, 5, 11, 3, 14, 10, 0, 6, 13,
        ],
        [
            15, 1, 8, 14, 6, 11, 3, 4, 9, 7, 2, 13, 12, 0, 5, 10,
            3, 13, 4, 7, 15, 2, 8, 14, 12, 0, 1, 10, 6, 9, 11, 5,
            0, 14, 7, 11, 10, 4, 13, 1, 5, 8, 12, 6, 9, 3, 2, 15,
            13, 8, 10, 1, 3, 15, 4, 2, 11, 6, 7, 12, 0, 5, 14, 9,
        ],
        [
            10, 0, 9, 14, 6, 3, 15, 5, 1, 13, 12, 7, 11, 4, 2, 8,
            13, 7, 0, 9, 3, 4, 6, 10, 2, 8, 5, 14, 12, 11, 15, 1,
            13, 6, 4, 9, 8, 15, 3, 0, 11, 1, 2, 12, 5, 10, 14, 7,
            1, 10, 13, 0, 6, 9, 8, 7, 4, 15, 14, 3, 11, 5, 2, 12,
        ],
        [
            7, 13, 14, 3, 0, 6, 9, 10, 1, 2, 8, 5, 11, 12, 4, 15,
            13, 8, 11, 5, 6, 15, 0, 3, 4, 7, 2, 12, 1, 10, 14, 9,
            10, 6, 9, 0, 12, 11, 7, 13, 15, 1, 3, 14, 5, 2, 8, 4,
            3, 15, 0, 6, 10, 1, 13, 8, 9, 4, 5, 11, 12, 7, 2, 14,
        ],
        [
            2, 12, 4, 1, 7, 10, 11, 6, 8, 5, 3, 15, 13, 0, 14, 9,
            14, 11, 2, 12, 4, 7, 13, 1, 5, 0, 15, 10, 3, 9, 8, 6,
            4, 2, 1, 11, 10, 13, 7, 8, 15, 9, 12, 5, 6, 3, 0, 14,
            11, 8, 12, 7, 1, 14, 2, 13, 6, 15, 0, 9, 10, 4, 5, 3,
        ],
        [
            12, 1, 10, 15, 9, 2, 6, 8, 0, 13, 3, 4, 14, 7, 5, 11,
            10, 15, 4, 2, 7, 12, 9, 5, 6, 1, 13, 14, 0, 11, 3, 8,
            9, 14, 15, 5, 2, 8, 12, 3, 7, 0, 4, 10, 1, 13, 11, 6,
            4, 3, 2, 12, 9, 5, 15, 10, 11, 14, 1, 7, 6, 0, 8, 13,
        ],
        [
            4, 11, 2, 14, 15, 0, 8, 13, 3, 12, 9, 7, 5, 10, 6, 1,
            13, 0, 11, 7, 4, 9, 1, 10, 14, 3, 5, 12, 2, 15, 8, 6,
            1, 4, 11, 13, 12, 3, 7, 14, 10, 15, 6, 8, 0, 5, 9, 2,
            6, 11, 13, 8, 1, 4, 10, 7, 9, 5, 0, 15, 14, 2, 3, 12,
        ],
        [
            13, 2, 8, 4, 6, 15, 11, 1, 10, 9, 3, 14, 5, 0, 12, 7,
            1, 15, 13, 8, 10, 3, 7, 4, 12, 5, 6, 11, 0, 14, 9, 2,
            7, 11, 4, 1, 9, 12, 14, 2, 0, 6, 10, 13, 15, 3, 5, 8,
            2, 1, 14, 7, 4, 10, 8, 13, 15, 12, 9, 0, 3, 5, 6, 11,
        ],
    ];

    /// <summary>
    /// Encrypts <paramref name="plaintext" /> under <paramref name="key" /> in one DES-ECB step.
    /// </summary>
    /// <param name="key">The 8-byte key; each byte's low bit, the parity bit, is ignored.</param>
    /// <param name="plaintext">The 8-byte block.</param>
    /// <returns>The 8-byte ciphertext.</returns>
    /// <exception cref="ArgumentException">A key or block that is not eight bytes long.</exception>
    public static byte[] EncryptBlock(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
    {
        if (key.Length != KeyLength)
        {
            throw new ArgumentException($"A DES key is {KeyLength} bytes.", nameof(key));
        }

        if (plaintext.Length != BlockLength)
        {
            throw new ArgumentException($"A DES block is {BlockLength} bytes.", nameof(plaintext));
        }

        ulong[] roundKeys = ScheduleRoundKeys(BinaryPrimitives.ReadUInt64BigEndian(key));
        ulong block = Permute(BinaryPrimitives.ReadUInt64BigEndian(plaintext), 64, InitialPermutation);
        uint left = (uint)(block >> 32);
        uint right = (uint)block;
        foreach (ulong roundKey in roundKeys)
        {
            (left, right) = (right, left ^ Feistel(right, roundKey));
        }

        byte[] ciphertext = new byte[BlockLength];
        BinaryPrimitives.WriteUInt64BigEndian(ciphertext, Permute(((ulong)right << 32) | left, 64, FinalPermutation));
        return ciphertext;
    }

    private static ulong[] ScheduleRoundKeys(ulong key)
    {
        ulong halves = Permute(key, 64, PermutedChoice1);
        uint c = (uint)(halves >> 28) & TwentyEightBits;
        uint d = (uint)halves & TwentyEightBits;
        ulong[] roundKeys = new ulong[RoundCount];
        for (int round = 0; round < RoundCount; round++)
        {
            c = RotateLeft28(c, KeyShifts[round]);
            d = RotateLeft28(d, KeyShifts[round]);
            roundKeys[round] = Permute(((ulong)c << 28) | d, 56, PermutedChoice2);
        }

        return roundKeys;
    }

    private static uint RotateLeft28(uint value, int count) =>
        ((value << count) | (value >> (28 - count))) & TwentyEightBits;

    private static uint Feistel(uint right, ulong roundKey)
    {
        ulong mixed = Permute(right, 32, Expansion) ^ roundKey;
        uint substituted = 0;
        for (int box = 0; box < SubstitutionBoxes.Length; box++)
        {
            int six = (int)(mixed >> (42 - (6 * box))) & 0x3F;
            int row = ((six & 0x20) >> 4) | (six & 1);
            int column = (six >> 1) & 0xF;
            substituted = (substituted << 4) | SubstitutionBoxes[box][(row * 16) + column];
        }

        return (uint)Permute(substituted, 32, Permutation);
    }

    private static ulong Permute(ulong input, int inputBits, byte[] table)
    {
        ulong output = 0;
        foreach (byte position in table)
        {
            output = (output << 1) | ((input >> (inputBits - position)) & 1);
        }

        return output;
    }
}
