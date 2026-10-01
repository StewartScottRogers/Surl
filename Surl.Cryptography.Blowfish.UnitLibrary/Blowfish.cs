using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Surl.Cryptography.Blowfish;

/// <summary>
/// The Blowfish block cipher (Schneier, 1993): its keyed state - the 18-word P-array and the
/// four 256-word S-boxes - with the cipher's 16 rounds over one 8-byte block and the key
/// expansions of OpenBSD's <c>blf.c</c>: the standard key schedule
/// (<see cref="ExpandKey(ReadOnlySpan{byte})" />, <c>Blowfish_expand0state</c>) and the
/// salted one bcrypt adds (<see cref="ExpandKey(ReadOnlySpan{byte}, ReadOnlySpan{byte})" />,
/// <c>Blowfish_expandstate</c>). A block's halves are big-endian words, as Blowfish is
/// specified. Chaining modes are the caller's: <c>blowfish-cbc</c> (RFC 4253 section 6.3) is
/// composed from <see cref="EncryptBlock" /> and <see cref="DecryptBlock" /> in
/// <c>Surl.Protocol.Ssh</c>.
/// </summary>
/// <remarks>
/// The base class library has no Blowfish, so it is built by hand (ADR-0061). Not
/// constant-time: every round indexes the S-boxes with key- and data-dependent bytes, as
/// Blowfish is specified. <see cref="Clear" /> zeroes the state.
/// </remarks>
public sealed class Blowfish
{
    /// <summary>The length in bytes of one Blowfish block.</summary>
    public const int BlockSize = 8;

    /// <summary>The length in bytes of the shortest key Blowfish accepts (Schneier, 1993).</summary>
    public const int MinimumKeySize = 4;

    /// <summary>The length in bytes of the longest key Blowfish accepts (Schneier, 1993).</summary>
    public const int MaximumKeySize = 56;

    private const int Rounds = 16;

    private readonly uint[] subkeys = new uint[Rounds + 2];

    private readonly uint[] boxes = new uint[4 * 256];

    /// <summary>
    /// Creates an unkeyed Blowfish: the P-array and S-boxes hold the digits of pi, ready for
    /// <see cref="ExpandKey(ReadOnlySpan{byte})" /> or bcrypt's salted schedule.
    /// </summary>
    public Blowfish() => Initialize();

    /// <summary>Creates a Blowfish keyed by the standard key schedule over <paramref name="key" />.</summary>
    /// <param name="key">The key, <see cref="MinimumKeySize" /> to <see cref="MaximumKeySize" /> bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="key" /> is shorter than <see cref="MinimumKeySize" /> or longer than <see cref="MaximumKeySize" /> bytes.</exception>
    public Blowfish(ReadOnlySpan<byte> key)
    {
        if (key.Length is < MinimumKeySize or > MaximumKeySize)
        {
            throw new ArgumentException(
                $"A Blowfish key is {MinimumKeySize} to {MaximumKeySize} bytes, not {key.Length}.",
                nameof(key));
        }

        Initialize();
        ExpandKey(key);
    }

    /// <summary>Loads the digits of pi into the P-array and S-boxes, before any key.</summary>
    public void Initialize()
    {
        BlowfishPiDigits.Subkeys.CopyTo(subkeys);
        BlowfishPiDigits.SubstitutionBoxes.CopyTo(boxes);
    }

    /// <summary>
    /// Blowfish's key schedule (<c>Blowfish_expand0state</c>): exclusive-ors
    /// <paramref name="key" />, cycled, into the P-array, then replaces the P-array and
    /// S-boxes with successive encryptions of an all-zero block. Any length is streamed
    /// cyclically, as bcrypt's 64-byte keys need; empty is an all-zero key.
    /// </summary>
    public void ExpandKey(ReadOnlySpan<byte> key) => ExpandKey(default, key);

    /// <summary>
    /// bcrypt's salted key schedule (<c>Blowfish_expandstate</c>): as
    /// <see cref="ExpandKey(ReadOnlySpan{byte})" />, but each block is exclusive-ored with
    /// the next eight bytes of <paramref name="data" />, cycled, before it is encrypted.
    /// Empty <paramref name="data" /> is the unsalted schedule.
    /// </summary>
    public void ExpandKey(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
    {
        int keyPosition = 0;
        for (int index = 0; index < subkeys.Length; index++)
        {
            subkeys[index] ^= ReadWord(key, ref keyPosition);
        }

        uint left = 0;
        uint right = 0;
        int dataPosition = 0;
        ReplaceWithEncryptions(subkeys, data, ref dataPosition, ref left, ref right);
        ReplaceWithEncryptions(boxes, data, ref dataPosition, ref left, ref right);
    }

    /// <summary>
    /// Encrypts the <see cref="BlockSize" />-byte block <paramref name="source" /> into
    /// <paramref name="destination" />, which may be the same span.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="source" /> is not one block, or <paramref name="destination" /> is shorter than one.</exception>
    public void EncryptBlock(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireOneBlock(source, destination);
        uint left = BinaryPrimitives.ReadUInt32BigEndian(source);
        uint right = BinaryPrimitives.ReadUInt32BigEndian(source[4..]);
        Encrypt(ref left, ref right);
        WriteBlock(left, right, destination);
    }

    /// <summary>
    /// Decrypts the <see cref="BlockSize" />-byte block <paramref name="source" /> into
    /// <paramref name="destination" />, which may be the same span.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="source" /> is not one block, or <paramref name="destination" /> is shorter than one.</exception>
    public void DecryptBlock(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        RequireOneBlock(source, destination);
        uint left = BinaryPrimitives.ReadUInt32BigEndian(source);
        uint right = BinaryPrimitives.ReadUInt32BigEndian(source[4..]);
        Decrypt(ref left, ref right);
        WriteBlock(left, right, destination);
    }

    /// <summary>Encrypts the block (<paramref name="left" />, <paramref name="right" />) in place.</summary>
    public void Encrypt(ref uint left, ref uint right)
    {
        uint x = left ^ subkeys[0];
        uint y = right;
        for (int round = 1; round <= Rounds; round += 2)
        {
            y ^= Mix(x) ^ subkeys[round];
            x ^= Mix(y) ^ subkeys[round + 1];
        }

        left = y ^ subkeys[Rounds + 1];
        right = x;
    }

    /// <summary>Decrypts the block (<paramref name="left" />, <paramref name="right" />) in place.</summary>
    public void Decrypt(ref uint left, ref uint right)
    {
        uint x = left ^ subkeys[Rounds + 1];
        uint y = right;
        for (int round = Rounds; round >= 1; round -= 2)
        {
            y ^= Mix(x) ^ subkeys[round];
            x ^= Mix(y) ^ subkeys[round - 1];
        }

        left = y ^ subkeys[0];
        right = x;
    }

    /// <summary>Zeroes the P-array and S-boxes.</summary>
    public void Clear()
    {
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(subkeys.AsSpan()));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(boxes.AsSpan()));
    }

    /// <summary>
    /// The next four bytes of <paramref name="data" /> as a big-endian word, wrapping to
    /// its start when they run out (<c>Blowfish_stream2word</c>). Empty data reads as zero.
    /// </summary>
    public static uint ReadWord(ReadOnlySpan<byte> data, ref int position)
    {
        if (data.IsEmpty)
        {
            return 0;
        }

        uint word = 0;
        for (int count = 0; count < 4; count++)
        {
            position %= data.Length;
            word = (word << 8) | data[position];
            position++;
        }

        return word;
    }

    private static void RequireOneBlock(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length != BlockSize)
        {
            throw new ArgumentException($"A Blowfish block is {BlockSize} bytes, not {source.Length}.", nameof(source));
        }

        if (destination.Length < BlockSize)
        {
            throw new ArgumentException($"A Blowfish block needs {BlockSize} bytes of room, not {destination.Length}.", nameof(destination));
        }
    }

    private static void WriteBlock(uint left, uint right, Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt32BigEndian(destination, left);
        BinaryPrimitives.WriteUInt32BigEndian(destination[4..], right);
    }

    private void ReplaceWithEncryptions(uint[] words, ReadOnlySpan<byte> data, ref int dataPosition, ref uint left, ref uint right)
    {
        for (int index = 0; index < words.Length; index += 2)
        {
            left ^= ReadWord(data, ref dataPosition);
            right ^= ReadWord(data, ref dataPosition);
            Encrypt(ref left, ref right);
            words[index] = left;
            words[index + 1] = right;
        }
    }

    /// <summary>Blowfish's F function: ((S1[a] + S2[b]) ^ S3[c]) + S4[d] over the bytes of <paramref name="half" />.</summary>
    private uint Mix(uint half) =>
        ((boxes[(int)(half >> 24)] + boxes[256 + (int)((half >> 16) & 0xFF)]) ^ boxes[512 + (int)((half >> 8) & 0xFF)])
        + boxes[768 + (int)(half & 0xFF)];
}
