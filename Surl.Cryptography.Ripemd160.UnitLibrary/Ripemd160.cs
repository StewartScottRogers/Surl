using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Surl.Cryptography.Ripemd160;

/// <summary>
/// The RIPEMD-160 hash function (Dobbertin, Bosselaers and Preneel, 1996): any message gives
/// a 20-byte hash. HMAC-RIPEMD-160 (RFC 2286) is built on it by <see cref="HmacRipemd160" />.
/// </summary>
/// <remarks>
/// The base class library has no RIPEMD-160, so it is built by hand (ADR-0061). Hash a whole
/// message with <see cref="HashData(ReadOnlySpan{byte})" />, or feed it in pieces with <see cref="AppendData" />
/// and end it with <see cref="GetHashAndReset" />; both give the same bytes. The message is
/// padded as MD4's is (a <c>0x80</c> byte, zeros to 56 bytes into a 64-byte block, and the
/// length in bits as a little-endian 64-bit number), and words are read and the hash written
/// little-endian. No branch or index depends on the message, only on its length. The state,
/// the buffered bytes and the message words are zeroed after use and by <see cref="Dispose" />.
/// </remarks>
public sealed class Ripemd160 : IDisposable
{
    /// <summary>The length in bytes of a hash.</summary>
    public const int HashSize = 20;

    /// <summary>The length in bytes of a block the compression function takes.</summary>
    public const int BlockSize = 64;

    private const int StateWords = HashSize / sizeof(uint);

    private const int BlockWords = BlockSize / sizeof(uint);

    private const int LengthOffset = BlockSize - sizeof(ulong);

    private const int Steps = 80;

    private const int StepsPerRound = 16;

    private readonly uint[] state = new uint[StateWords];

    private readonly byte[] buffer = new byte[BlockSize];

    private int bufferLength;

    private ulong messageLength;

    private bool disposed;

    /// <summary>Starts an empty message.</summary>
    public Ripemd160()
    {
        Initialize(state);
    }

    /// <summary>The message word each step of the left line adds (r).</summary>
    private static ReadOnlySpan<byte> LeftWordOrder =>
    [
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        7, 4, 13, 1, 10, 6, 15, 3, 12, 0, 9, 5, 2, 14, 11, 8,
        3, 10, 14, 4, 9, 15, 8, 1, 2, 7, 0, 6, 13, 11, 5, 12,
        1, 9, 11, 10, 0, 8, 12, 4, 13, 3, 7, 15, 14, 5, 6, 2,
        4, 0, 5, 9, 7, 12, 2, 10, 14, 1, 3, 8, 11, 6, 15, 13,
    ];

    /// <summary>The message word each step of the right line adds (r').</summary>
    private static ReadOnlySpan<byte> RightWordOrder =>
    [
        5, 14, 7, 0, 9, 2, 11, 4, 13, 6, 15, 8, 1, 10, 3, 12,
        6, 11, 3, 7, 0, 13, 5, 10, 14, 15, 8, 12, 4, 9, 1, 2,
        15, 5, 1, 3, 7, 14, 6, 9, 11, 8, 12, 2, 10, 0, 4, 13,
        8, 6, 4, 1, 3, 11, 15, 0, 5, 12, 2, 13, 9, 7, 10, 14,
        12, 15, 10, 4, 1, 5, 8, 7, 6, 2, 13, 14, 0, 3, 9, 11,
    ];

    /// <summary>The left rotation of each step of the left line (s).</summary>
    private static ReadOnlySpan<byte> LeftRotations =>
    [
        11, 14, 15, 12, 5, 8, 7, 9, 11, 13, 14, 15, 6, 7, 9, 8,
        7, 6, 8, 13, 11, 9, 7, 15, 7, 12, 15, 9, 11, 7, 13, 12,
        11, 13, 6, 7, 14, 9, 13, 15, 14, 8, 13, 6, 5, 12, 7, 5,
        11, 12, 14, 15, 14, 15, 9, 8, 9, 14, 5, 6, 8, 6, 5, 12,
        9, 15, 5, 11, 6, 8, 13, 12, 5, 12, 13, 14, 11, 8, 5, 6,
    ];

    /// <summary>The left rotation of each step of the right line (s').</summary>
    private static ReadOnlySpan<byte> RightRotations =>
    [
        8, 9, 9, 11, 13, 15, 15, 5, 7, 7, 8, 11, 14, 14, 12, 6,
        9, 13, 15, 7, 12, 8, 9, 11, 7, 7, 12, 7, 6, 15, 13, 11,
        9, 7, 15, 11, 8, 6, 6, 14, 12, 13, 5, 14, 13, 13, 7, 5,
        15, 5, 8, 11, 14, 14, 6, 14, 6, 9, 12, 9, 12, 5, 15, 8,
        8, 5, 12, 9, 12, 5, 14, 6, 8, 13, 6, 5, 15, 13, 11, 11,
    ];

    /// <summary>The constant each round of the left line adds (K).</summary>
    private static ReadOnlySpan<uint> LeftConstants => [0x00000000, 0x5a827999, 0x6ed9eba1, 0x8f1bbcdc, 0xa953fd4e];

    /// <summary>The constant each round of the right line adds (K').</summary>
    private static ReadOnlySpan<uint> RightConstants => [0x50a28be6, 0x5c4dd124, 0x6d703ef3, 0x7a6d76e9, 0x00000000];

    /// <summary>Writes the RIPEMD-160 hash of <paramref name="source" /> to <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not <see cref="HashSize" /> bytes.</exception>
    public static void HashData(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        using var ripemd160 = new Ripemd160();
        ripemd160.AppendData(source);
        ripemd160.GetHashAndReset(destination);
    }

    /// <summary>Returns the 20-byte RIPEMD-160 hash of <paramref name="source" />.</summary>
    public static byte[] HashData(ReadOnlySpan<byte> source)
    {
        var hash = new byte[HashSize];
        HashData(source, hash);
        return hash;
    }

    /// <summary>Adds <paramref name="data" /> to the message being hashed.</summary>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void AppendData(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        messageLength += (ulong)data.Length;
        if (bufferLength > 0)
        {
            var taken = Math.Min(BlockSize - bufferLength, data.Length);
            data[..taken].CopyTo(buffer.AsSpan(bufferLength));
            bufferLength += taken;
            data = data[taken..];
            if (bufferLength < BlockSize)
            {
                return;
            }

            Compress(state, buffer);
            bufferLength = 0;
        }

        for (; data.Length >= BlockSize; data = data[BlockSize..])
        {
            Compress(state, data[..BlockSize]);
        }

        data.CopyTo(buffer);
        bufferLength = data.Length;
    }

    /// <summary>
    /// Pads the message, writes its hash to <paramref name="destination" /> and starts a new,
    /// empty message.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not <see cref="HashSize" /> bytes.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void GetHashAndReset(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (destination.Length != HashSize)
        {
            throw new ArgumentException($"RIPEMD-160 needs a {HashSize}-byte destination; this is {destination.Length}.", nameof(destination));
        }

        buffer[bufferLength++] = 0x80;
        if (bufferLength > LengthOffset)
        {
            buffer.AsSpan(bufferLength).Clear();
            Compress(state, buffer);
            bufferLength = 0;
        }

        buffer.AsSpan(bufferLength, LengthOffset - bufferLength).Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(LengthOffset), messageLength << 3);
        Compress(state, buffer);
        for (var word = 0; word < StateWords; word++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination[(word * sizeof(uint))..], state[word]);
        }

        Clear();
        Initialize(state);
    }

    /// <summary>Zeroes the state and any buffered data.</summary>
    public void Dispose()
    {
        Clear();
        disposed = true;
    }

    private static void Initialize(Span<uint> state)
    {
        state[0] = 0x67452301;
        state[1] = 0xefcdab89;
        state[2] = 0x98badcfe;
        state[3] = 0x10325476;
        state[4] = 0xc3d2e1f0;
    }

    /// <summary>
    /// The compression function: the left and right lines of 80 steps each over the 16
    /// little-endian words of <paramref name="block" />, combined into <paramref name="state" />.
    /// </summary>
    private static void Compress(Span<uint> state, ReadOnlySpan<byte> block)
    {
        Span<uint> words = stackalloc uint[BlockWords];
        Span<uint> left = stackalloc uint[StateWords];
        Span<uint> right = stackalloc uint[StateWords];
        try
        {
            for (var word = 0; word < BlockWords; word++)
            {
                words[word] = BinaryPrimitives.ReadUInt32LittleEndian(block[(word * sizeof(uint))..]);
            }

            state.CopyTo(left);
            state.CopyTo(right);
            for (var step = 0; step < Steps; step++)
            {
                var round = step / StepsPerRound;
                Step(left, RoundFunction(round, left[1], left[2], left[3]) + words[LeftWordOrder[step]] + LeftConstants[round], LeftRotations[step]);
                Step(right, RoundFunction(4 - round, right[1], right[2], right[3]) + words[RightWordOrder[step]] + RightConstants[round], RightRotations[step]);
            }

            var combined = state[1] + left[2] + right[3];
            state[1] = state[2] + left[3] + right[4];
            state[2] = state[3] + left[4] + right[0];
            state[3] = state[4] + left[0] + right[1];
            state[4] = state[0] + left[1] + right[2];
            state[0] = combined;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(words));
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(left));
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(right));
        }
    }

    /// <summary>
    /// One step of a line whose registers A to E are <paramref name="line" />:
    /// T = rol(A + <paramref name="addend" />, <paramref name="rotation" />) + E, then
    /// A = E, E = D, D = rol(C, 10), C = B, B = T.
    /// </summary>
    private static void Step(Span<uint> line, uint addend, int rotation)
    {
        var t = BitOperations.RotateLeft(line[0] + addend, rotation) + line[4];
        line[0] = line[4];
        line[4] = line[3];
        line[3] = BitOperations.RotateLeft(line[2], 10);
        line[2] = line[1];
        line[1] = t;
    }

    /// <summary>The five boolean functions f1 to f5, numbered 0 to 4.</summary>
    private static uint RoundFunction(int round, uint x, uint y, uint z) => round switch
    {
        0 => x ^ y ^ z,
        1 => (x & y) | (~x & z),
        2 => (x | ~y) ^ z,
        3 => (x & z) | (y & ~z),
        _ => x ^ (y | ~z),
    };

    /// <summary>Zeroes the state, the buffered bytes and the length.</summary>
    private void Clear()
    {
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(state.AsSpan()));
        CryptographicOperations.ZeroMemory(buffer);
        bufferLength = 0;
        messageLength = 0;
    }
}
