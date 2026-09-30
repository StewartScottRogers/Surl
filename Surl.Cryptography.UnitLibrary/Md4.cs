using System.Buffers.Binary;

namespace Surl.Cryptography;

/// <summary>
/// Computes MD4 (RFC 1320), which the base class library does not provide, so NTLM can derive
/// the NT hash of a password: <c>MD4(UTF-16LE(password))</c> ([MS-NLMP] section 3.3.1).
/// </summary>
/// <remarks>
/// MD4 is broken as a general-purpose hash; use it only where NTLM requires it. Every expected
/// hash in the tests comes from RFC 1320 appendix A.5 and [MS-NLMP] section 4.2.2.1.2.
/// </remarks>
public static class Md4
{
    /// <summary>The length of an MD4 hash, in bytes.</summary>
    public const int HashLength = 16;

    private const int BlockLength = 64;

    private const int LengthFieldLength = 8;

    private static readonly uint[] InitialState = [0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476];

    // Per round: the order the sixteen message words are taken in, and the four shifts.
    private static readonly int[][] WordOrder =
    [
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15],
        [0, 4, 8, 12, 1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15],
        [0, 8, 4, 12, 2, 10, 6, 14, 1, 9, 5, 13, 3, 11, 7, 15],
    ];

    private static readonly int[][] Shifts =
    [
        [3, 7, 11, 19],
        [3, 5, 9, 13],
        [3, 9, 11, 15],
    ];

    private static readonly uint[] RoundConstants = [0x00000000, 0x5A827999, 0x6ED9EBA1];

    /// <summary>
    /// Computes the MD4 hash of <paramref name="message" />.
    /// </summary>
    /// <param name="message">The bytes to hash.</param>
    /// <returns>The 16-byte hash.</returns>
    public static byte[] HashData(ReadOnlySpan<byte> message)
    {
        Span<uint> state = stackalloc uint[4];
        InitialState.CopyTo(state);

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
        for (int word = 0; word < 4; word++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(hash.AsSpan(word * 4), state[word]);
        }

        return hash;
    }

    // Writes the last partial block, a 0x80 byte, zeros to 56 mod 64 and the message length
    // in bits as a 64-bit little-endian number into tail, and returns the bytes used: one
    // block, or two when the partial block leaves less than 9 bytes free.
    private static int PadTail(ReadOnlySpan<byte> partialBlock, ulong messageLength, Span<byte> tail)
    {
        tail.Clear();
        partialBlock.CopyTo(tail);
        tail[partialBlock.Length] = 0x80;
        int tailLength = partialBlock.Length + 1 + LengthFieldLength <= BlockLength ? BlockLength : 2 * BlockLength;
        BinaryPrimitives.WriteUInt64LittleEndian(tail[(tailLength - LengthFieldLength)..], messageLength * 8);
        return tailLength;
    }

    private static void Compress(Span<uint> state, ReadOnlySpan<byte> block)
    {
        Span<uint> words = stackalloc uint[16];
        for (int i = 0; i < 16; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(block[(i * 4)..]);
        }

        Span<uint> working = stackalloc uint[4];
        state.CopyTo(working);
        for (int round = 0; round < 3; round++)
        {
            for (int step = 0; step < 16; step++)
            {
                Step(working, round, words[WordOrder[round][step]], Shifts[round][step % 4], step % 4);
            }
        }

        for (int i = 0; i < 4; i++)
        {
            state[i] += working[i];
        }
    }

    // One MD4 operation. The register it updates cycles a, d, c, b (working[0], [3], [2], [1]),
    // and the round function takes the three registers that follow it, in order.
    private static void Step(Span<uint> working, int round, uint word, int shift, int position)
    {
        int target = (4 - position) % 4;
        uint x = working[(target + 1) % 4], y = working[(target + 2) % 4], z = working[(target + 3) % 4];
        uint mixed = RoundFunction(round, x, y, z);
        working[target] = uint.RotateLeft(working[target] + mixed + word + RoundConstants[round], shift);
    }

    // F (round 1) selects, G (round 2) takes the majority and H (round 3) is parity.
    private static uint RoundFunction(int round, uint x, uint y, uint z) => round switch
    {
        0 => (x & y) | (~x & z),
        1 => (x & y) | (x & z) | (y & z),
        _ => x ^ y ^ z,
    };
}
