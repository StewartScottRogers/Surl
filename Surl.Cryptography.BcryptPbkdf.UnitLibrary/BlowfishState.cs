using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Surl.Cryptography.BcryptPbkdf;

/// <summary>
/// Blowfish's keyed state - the 18-word P-array and the four 256-word S-boxes - with the
/// cipher's 16 rounds and the key expansions of OpenBSD's <c>blf.c</c>: the standard key
/// schedule (<see cref="ExpandKey(ReadOnlySpan{byte})" />, <c>Blowfish_expand0state</c>)
/// and the salted one bcrypt adds (<see cref="ExpandKey(ReadOnlySpan{byte}, ReadOnlySpan{byte})" />,
/// <c>Blowfish_expandstate</c>).
/// </summary>
/// <remarks>
/// Not constant-time: every round indexes the S-boxes with key- and data-dependent bytes,
/// as Blowfish is specified. <see cref="Clear" /> zeroes the state.
/// </remarks>
internal sealed class BlowfishState
{
    private const int Rounds = 16;

    private readonly uint[] subkeys = new uint[Rounds + 2];

    private readonly uint[] boxes = new uint[4 * 256];

    /// <summary>Loads the digits of pi into the P-array and S-boxes, before any key.</summary>
    internal void Initialize()
    {
        BlowfishPiDigits.Subkeys.CopyTo(subkeys);
        BlowfishPiDigits.SubstitutionBoxes.CopyTo(boxes);
    }

    /// <summary>
    /// Blowfish's key schedule (<c>Blowfish_expand0state</c>): exclusive-ors
    /// <paramref name="key" />, cycled, into the P-array, then replaces the P-array and
    /// S-boxes with successive encryptions of an all-zero block.
    /// </summary>
    internal void ExpandKey(ReadOnlySpan<byte> key) => ExpandKey(default, key);

    /// <summary>
    /// bcrypt's salted key schedule (<c>Blowfish_expandstate</c>): as
    /// <see cref="ExpandKey(ReadOnlySpan{byte})" />, but each block is exclusive-ored with
    /// the next eight bytes of <paramref name="data" />, cycled, before it is encrypted.
    /// Empty <paramref name="data" /> is the unsalted schedule.
    /// </summary>
    internal void ExpandKey(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
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

    /// <summary>Encrypts the block (<paramref name="left" />, <paramref name="right" />) in place.</summary>
    internal void Encrypt(ref uint left, ref uint right)
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
    internal void Decrypt(ref uint left, ref uint right)
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
    internal void Clear()
    {
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(subkeys.AsSpan()));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(boxes.AsSpan()));
    }

    /// <summary>
    /// The next four bytes of <paramref name="data" /> as a big-endian word, wrapping to
    /// its start when they run out (<c>Blowfish_stream2word</c>). Empty data reads as zero.
    /// </summary>
    internal static uint ReadWord(ReadOnlySpan<byte> data, ref int position)
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
