using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Surl.Cryptography.Cast128;

/// <summary>
/// The CAST-128 block cipher (RFC 2144): 64-bit blocks, a key of 5 to 16 bytes (40 to 128
/// bits) zero-padded on the right to 16, 12 rounds for a key of at most 10 bytes and 16 for
/// a longer one (section 2.5). <see cref="EncryptBlock" /> and <see cref="DecryptBlock" />
/// transform one block; the cipher block chaining SSH's <c>cast128-cbc</c> runs on it
/// (RFC 4253 section 6.3) is <c>Surl.Protocol.Ssh</c>'s. Blocks are read and written
/// big-endian, as RFC 2144's test vectors are.
/// </summary>
/// <remarks>
/// The base class library has no CAST-128, so it is built by hand (ADR-0061). Not
/// constant-time: CAST-128 indexes its S-boxes with key- and data-dependent bytes by design;
/// it exists only so Surl can answer an upstream curl whose OpenSSL build still offers
/// <c>cast128-cbc</c>. The 32 subkeys are held by the instance and zeroed by
/// <see cref="Dispose" />.
/// </remarks>
public sealed class Cast128 : IDisposable
{
    /// <summary>The length in bytes of a block.</summary>
    public const int BlockSize = 8;

    /// <summary>The shortest key in bytes: 40 bits.</summary>
    public const int MinimumKeySize = 5;

    /// <summary>The longest key in bytes: 128 bits.</summary>
    public const int MaximumKeySize = 16;

    private const int LongestShortKey = 10;

    private const int FullRounds = 16;

    private const int ShortKeyRounds = 12;

    private const int BoxLength = 256;

    // Buffer offsets of RFC 2144's x0..xF and z0..zF during the key schedule.
    private const int X = 0;
    private const int Z = 16;

    private const byte Subkey = 0xFF;

    private readonly uint[] subkeys = new uint[2 * FullRounds];

    private readonly int rounds;

    private bool disposed;

    /// <summary>Runs CAST-128's key schedule on <paramref name="key" />, zero-padded to 16 bytes.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="key" /> is shorter than <see cref="MinimumKeySize" /> or longer than
    /// <see cref="MaximumKeySize" /> bytes.
    /// </exception>
    public Cast128(ReadOnlySpan<byte> key)
    {
        if (key.Length < MinimumKeySize || key.Length > MaximumKeySize)
        {
            throw new ArgumentException($"CAST-128 needs a key of {MinimumKeySize} to {MaximumKeySize} bytes; this is {key.Length}.", nameof(key));
        }

        rounds = key.Length <= LongestShortKey ? ShortKeyRounds : FullRounds;
        ScheduleKey(key, subkeys);
    }

    /// <summary>
    /// RFC 2144 section 2.4 as a table: each row is (destination, source, a, b, c, d, box,
    /// e) over a buffer holding x0..xF then z0..zF. A row with destination
    /// <see cref="Subkey" /> emits the next subkey S5[a] ^ S6[b] ^ S7[c] ^ S8[d] ^ Sbox[e];
    /// any other row writes the word at source exclusive-ored with the same mix to the word
    /// at destination. The 32 rows give 16 subkeys; the schedule runs them twice.
    /// </summary>
    private static ReadOnlySpan<byte> KeyScheduleRows =>
    [
        // z0z1z2z3 .. zCzDzEzF from x.
        Z + 0x0, X + 0x0, X + 0xD, X + 0xF, X + 0xC, X + 0xE, 2, X + 0x8,
        Z + 0x4, X + 0x8, Z + 0x0, Z + 0x2, Z + 0x1, Z + 0x3, 3, X + 0xA,
        Z + 0x8, X + 0xC, Z + 0x7, Z + 0x6, Z + 0x5, Z + 0x4, 0, X + 0x9,
        Z + 0xC, X + 0x4, Z + 0xA, Z + 0x9, Z + 0xB, Z + 0x8, 1, X + 0xB,

        // K1..K4 (K17..K20).
        Subkey, 0, Z + 0x8, Z + 0x9, Z + 0x7, Z + 0x6, 0, Z + 0x2,
        Subkey, 0, Z + 0xA, Z + 0xB, Z + 0x5, Z + 0x4, 1, Z + 0x6,
        Subkey, 0, Z + 0xC, Z + 0xD, Z + 0x3, Z + 0x2, 2, Z + 0x9,
        Subkey, 0, Z + 0xE, Z + 0xF, Z + 0x1, Z + 0x0, 3, Z + 0xC,

        // x0x1x2x3 .. xCxDxExF from z.
        X + 0x0, Z + 0x8, Z + 0x5, Z + 0x7, Z + 0x4, Z + 0x6, 2, Z + 0x0,
        X + 0x4, Z + 0x0, X + 0x0, X + 0x2, X + 0x1, X + 0x3, 3, Z + 0x2,
        X + 0x8, Z + 0x4, X + 0x7, X + 0x6, X + 0x5, X + 0x4, 0, Z + 0x1,
        X + 0xC, Z + 0xC, X + 0xA, X + 0x9, X + 0xB, X + 0x8, 1, Z + 0x3,

        // K5..K8 (K21..K24).
        Subkey, 0, X + 0x3, X + 0x2, X + 0xC, X + 0xD, 0, X + 0x8,
        Subkey, 0, X + 0x1, X + 0x0, X + 0xE, X + 0xF, 1, X + 0xD,
        Subkey, 0, X + 0x7, X + 0x6, X + 0x8, X + 0x9, 2, X + 0x3,
        Subkey, 0, X + 0x5, X + 0x4, X + 0xA, X + 0xB, 3, X + 0x7,

        // z from x again.
        Z + 0x0, X + 0x0, X + 0xD, X + 0xF, X + 0xC, X + 0xE, 2, X + 0x8,
        Z + 0x4, X + 0x8, Z + 0x0, Z + 0x2, Z + 0x1, Z + 0x3, 3, X + 0xA,
        Z + 0x8, X + 0xC, Z + 0x7, Z + 0x6, Z + 0x5, Z + 0x4, 0, X + 0x9,
        Z + 0xC, X + 0x4, Z + 0xA, Z + 0x9, Z + 0xB, Z + 0x8, 1, X + 0xB,

        // K9..K12 (K25..K28).
        Subkey, 0, Z + 0x3, Z + 0x2, Z + 0xC, Z + 0xD, 0, Z + 0x9,
        Subkey, 0, Z + 0x1, Z + 0x0, Z + 0xE, Z + 0xF, 1, Z + 0xC,
        Subkey, 0, Z + 0x7, Z + 0x6, Z + 0x8, Z + 0x9, 2, Z + 0x2,
        Subkey, 0, Z + 0x5, Z + 0x4, Z + 0xA, Z + 0xB, 3, Z + 0x6,

        // x from z again.
        X + 0x0, Z + 0x8, Z + 0x5, Z + 0x7, Z + 0x4, Z + 0x6, 2, Z + 0x0,
        X + 0x4, Z + 0x0, X + 0x0, X + 0x2, X + 0x1, X + 0x3, 3, Z + 0x2,
        X + 0x8, Z + 0x4, X + 0x7, X + 0x6, X + 0x5, X + 0x4, 0, Z + 0x1,
        X + 0xC, Z + 0xC, X + 0xA, X + 0x9, X + 0xB, X + 0x8, 1, Z + 0x3,

        // K13..K16 (K29..K32).
        Subkey, 0, X + 0x8, X + 0x9, X + 0x7, X + 0x6, 0, X + 0x3,
        Subkey, 0, X + 0xA, X + 0xB, X + 0x5, X + 0x4, 1, X + 0x7,
        Subkey, 0, X + 0xC, X + 0xD, X + 0x3, X + 0x2, 2, X + 0x8,
        Subkey, 0, X + 0xE, X + 0xF, X + 0x1, X + 0x0, 3, X + 0xD,
    ];

    /// <summary>Encrypts the one block <paramref name="source" /> into <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException">A span is not <see cref="BlockSize" /> bytes.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void EncryptBlock(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireUsable(source.Length, destination.Length, BlockSize);
        TransformBlock(source, destination, encrypt: true);
    }

    /// <summary>Decrypts the one block <paramref name="source" /> into <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException">A span is not <see cref="BlockSize" /> bytes.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void DecryptBlock(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireUsable(source.Length, destination.Length, BlockSize);
        TransformBlock(source, destination, encrypt: false);
    }

    /// <summary>Zeroes the subkeys; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(subkeys.AsSpan()));
        disposed = true;
    }

    /// <summary>
    /// The round function f of RFC 2144 section 2.2 for round <paramref name="round" />
    /// (zero-based): type 1, 2 or 3 as the round's position modulo three.
    /// </summary>
    internal static uint Round(int round, uint data, uint maskingKey, int rotationKey)
    {
        ReadOnlySpan<uint> boxes = Cast128SubstitutionBoxes.RoundBoxes;
        int type = round % 3;
        uint combined = type switch
        {
            0 => maskingKey + data,
            1 => maskingKey ^ data,
            _ => maskingKey - data,
        };
        uint input = BitOperations.RotateLeft(combined, rotationKey);
        uint s1 = boxes[(int)(input >> 24)];
        uint s2 = boxes[BoxLength + (int)((input >> 16) & 0xFF)];
        uint s3 = boxes[(2 * BoxLength) + (int)((input >> 8) & 0xFF)];
        uint s4 = boxes[(3 * BoxLength) + (int)(input & 0xFF)];
        return type switch
        {
            0 => ((s1 ^ s2) - s3) + s4,
            1 => ((s1 - s2) + s3) ^ s4,
            _ => ((s1 + s2) ^ s3) - s4,
        };
    }

    private static void ScheduleKey(ReadOnlySpan<byte> key, Span<uint> destination)
    {
        Span<byte> buffer = stackalloc byte[32];
        try
        {
            buffer.Clear();
            key.CopyTo(buffer);
            ReadOnlySpan<byte> rows = KeyScheduleRows;
            int next = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int row = 0; row < rows.Length; row += 8)
                {
                    next = ApplyKeyScheduleRow(rows.Slice(row, 8), buffer, destination, next);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private static int ApplyKeyScheduleRow(ReadOnlySpan<byte> row, Span<byte> buffer, Span<uint> destination, int next)
    {
        ReadOnlySpan<uint> boxes = Cast128SubstitutionBoxes.KeyScheduleBoxes;
        uint mix = boxes[buffer[row[2]]]
            ^ boxes[BoxLength + buffer[row[3]]]
            ^ boxes[(2 * BoxLength) + buffer[row[4]]]
            ^ boxes[(3 * BoxLength) + buffer[row[5]]]
            ^ boxes[(row[6] * BoxLength) + buffer[row[7]]];
        if (row[0] == Subkey)
        {
            destination[next] = mix;
            return next + 1;
        }

        Span<byte> word = buffer.Slice(row[0], 4);
        BinaryPrimitives.WriteUInt32BigEndian(word, BinaryPrimitives.ReadUInt32BigEndian(buffer[row[1]..]) ^ mix);
        return next;
    }

    private static void RequireLength(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"CAST-128 needs {expected} bytes here; this is {length}.", parameterName);
        }
    }

    private void RequireUsable(int sourceLength, int destinationLength, int expectedSourceLength)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireLength(sourceLength, expectedSourceLength, "source");
        RequireLength(destinationLength, sourceLength, "destination");
    }

    private void TransformBlock(ReadOnlySpan<byte> source, Span<byte> destination, bool encrypt)
    {
        uint left = BinaryPrimitives.ReadUInt32BigEndian(source);
        uint right = BinaryPrimitives.ReadUInt32BigEndian(source[4..]);
        for (int step = 0; step < rounds; step++)
        {
            int round = encrypt ? step : rounds - 1 - step;
            uint mixed = left ^ Round(round, right, subkeys[round], (int)(subkeys[FullRounds + round] & 31));
            left = right;
            right = mixed;
        }

        BinaryPrimitives.WriteUInt32BigEndian(destination, right);
        BinaryPrimitives.WriteUInt32BigEndian(destination[4..], left);
    }
}
