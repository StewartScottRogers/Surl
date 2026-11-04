using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Surl.Cryptography.Poly1305;

/// <summary>
/// The Poly1305 one-time authenticator of RFC 8439 section 2.5: a 32-byte one-time key
/// (<c>r</c>, clamped as section 2.5.1 states, then <c>s</c>) and a message give a
/// 16-byte tag. A key must never authenticate two messages.
/// </summary>
/// <remarks>
/// Constant-time: the accumulator is five 26-bit limbs multiplied into 64-bit products,
/// carries are shifts and masks, and the final reduction modulo 2^130 - 5 selects by mask,
/// so no branch or index depends on the key or the accumulator. The state and every
/// temporary are zeroed before returning. Checking a received tag is the caller's job:
/// compare it with the computed one using
/// <see cref="CryptographicOperations.FixedTimeEquals" />, never with a loop that stops at
/// the first difference.
/// </remarks>
public static class Poly1305
{
    /// <summary>The length in bytes of a one-time key.</summary>
    public const int KeySize = 32;

    /// <summary>The length in bytes of a tag.</summary>
    public const int TagSize = 16;

    /// <summary>The words kept while a tag is computed: five limbs of r, four words of s, five limbs of the accumulator.</summary>
    private const int StateLength = 14;

    private const int BlockSize = 16;

    private const uint LimbMask = 0x3ffffff;

    /// <summary>2^128 in the top limb: the bit every full 16-byte block carries.</summary>
    private const uint FullBlockBit = 1u << 24;

    private const int R = 0;

    private const int S = 5;

    private const int H = 9;

    /// <summary>
    /// Computes the tag of <paramref name="message" /> under the one-time
    /// <paramref name="key" /> and returns it.
    /// </summary>
    /// <param name="key">The 32-byte one-time key, <c>r</c> then <c>s</c>.</param>
    /// <param name="message">The message to authenticate, of any length.</param>
    /// <returns>The 16-byte tag.</returns>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not 32 bytes.</exception>
    public static byte[] ComputeTag(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message)
    {
        byte[] tag = new byte[TagSize];
        ComputeTag(key, message, tag);
        return tag;
    }

    /// <summary>
    /// Computes the tag of <paramref name="message" /> under the one-time
    /// <paramref name="key" /> into <paramref name="tag" />.
    /// </summary>
    /// <param name="key">The 32-byte one-time key, <c>r</c> then <c>s</c>.</param>
    /// <param name="message">The message to authenticate, of any length.</param>
    /// <param name="tag">The 16 bytes the tag is written to.</param>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not 32 bytes or <paramref name="tag" /> is not 16.</exception>
    public static void ComputeTag(ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> tag)
    {
        RequireLength(key.Length, KeySize, nameof(key));
        RequireLength(tag.Length, TagSize, nameof(tag));
        Span<uint> state = stackalloc uint[StateLength];
        try
        {
            LoadKey(state, key);
            AbsorbMessage(state, message);
            WriteTag(state, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(state));
        }
    }

    /// <summary>
    /// Loads <c>r</c> clamped and split into 26-bit limbs, <c>s</c> as four words, and a
    /// zero accumulator.
    /// </summary>
    private static void LoadKey(Span<uint> state, ReadOnlySpan<byte> key)
    {
        state[R] = ReadWord(key, 0) & 0x3ffffff;
        state[R + 1] = (ReadWord(key, 3) >> 2) & 0x3ffff03;
        state[R + 2] = (ReadWord(key, 6) >> 4) & 0x3ffc0ff;
        state[R + 3] = (ReadWord(key, 9) >> 6) & 0x3f03fff;
        state[R + 4] = (ReadWord(key, 12) >> 8) & 0x00fffff;
        for (int word = 0; word < 4; word++)
        {
            state[S + word] = ReadWord(key, 16 + (word * sizeof(uint)));
        }

        state[H..].Clear();
    }

    /// <summary>
    /// Adds <paramref name="message" /> to the accumulator in 16-byte blocks, ending a
    /// short last block with a <c>0x01</c> byte and zeros as RFC 8439 section 2.5 specifies.
    /// </summary>
    private static void AbsorbMessage(Span<uint> state, ReadOnlySpan<byte> message)
    {
        int fullLength = message.Length & ~(BlockSize - 1);
        for (int offset = 0; offset < fullLength; offset += BlockSize)
        {
            AbsorbBlock(state, message.Slice(offset, BlockSize), FullBlockBit);
        }

        int remaining = message.Length - fullLength;
        if (remaining == 0)
        {
            return;
        }

        Span<byte> lastBlock = stackalloc byte[BlockSize];
        try
        {
            lastBlock.Clear();
            message[fullLength..].CopyTo(lastBlock);
            lastBlock[remaining] = 1;
            AbsorbBlock(state, lastBlock, 0);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(lastBlock);
        }
    }

    /// <summary>
    /// Reduces the accumulator fully modulo 2^130 - 5, adds <c>s</c> modulo 2^128 and
    /// writes the tag little-endian.
    /// </summary>
    private static void WriteTag(Span<uint> state, Span<byte> tag)
    {
        uint h0 = state[H], h1 = state[H + 1], h2 = state[H + 2], h3 = state[H + 3], h4 = state[H + 4];
        uint carry = h1 >> 26; h1 &= LimbMask;
        h2 += carry; carry = h2 >> 26; h2 &= LimbMask;
        h3 += carry; carry = h3 >> 26; h3 &= LimbMask;
        h4 += carry; carry = h4 >> 26; h4 &= LimbMask;
        h0 += carry * 5; carry = h0 >> 26; h0 &= LimbMask;
        h1 += carry;

        // g = h + 5 - 2^130; keep g when it did not go negative, that is when h >= p.
        uint g0 = h0 + 5; carry = g0 >> 26; g0 &= LimbMask;
        uint g1 = h1 + carry; carry = g1 >> 26; g1 &= LimbMask;
        uint g2 = h2 + carry; carry = g2 >> 26; g2 &= LimbMask;
        uint g3 = h3 + carry; carry = g3 >> 26; g3 &= LimbMask;
        uint g4 = h4 + carry - (1u << 26);
        uint keepG = (g4 >> 31) - 1;
        h0 = SelectByMask(keepG, g0, h0);
        h1 = SelectByMask(keepG, g1, h1);
        h2 = SelectByMask(keepG, g2, h2);
        h3 = SelectByMask(keepG, g3, h3);
        h4 = SelectByMask(keepG, g4, h4);

        ulong sum = (ulong)(h0 | (h1 << 26)) + state[S];
        BinaryPrimitives.WriteUInt32LittleEndian(tag, (uint)sum);
        sum = (ulong)((h1 >> 6) | (h2 << 20)) + state[S + 1] + (sum >> 32);
        BinaryPrimitives.WriteUInt32LittleEndian(tag[4..], (uint)sum);
        sum = (ulong)((h2 >> 12) | (h3 << 14)) + state[S + 2] + (sum >> 32);
        BinaryPrimitives.WriteUInt32LittleEndian(tag[8..], (uint)sum);
        sum = (ulong)((h3 >> 18) | (h4 << 8)) + state[S + 3] + (sum >> 32);
        BinaryPrimitives.WriteUInt32LittleEndian(tag[12..], (uint)sum);
    }

    /// <summary>
    /// h = (h + block + <paramref name="topBit" />) * r mod 2^130 - 5, partially reduced,
    /// on 26-bit limbs.
    /// </summary>
    private static void AbsorbBlock(Span<uint> state, ReadOnlySpan<byte> block, uint topBit)
    {
        ulong r0 = state[R], r1 = state[R + 1], r2 = state[R + 2], r3 = state[R + 3], r4 = state[R + 4];
        ulong s1 = r1 * 5, s2 = r2 * 5, s3 = r3 * 5, s4 = r4 * 5;
        ulong h0 = state[H] + (ReadWord(block, 0) & LimbMask);
        ulong h1 = state[H + 1] + ((ReadWord(block, 3) >> 2) & LimbMask);
        ulong h2 = state[H + 2] + ((ReadWord(block, 6) >> 4) & LimbMask);
        ulong h3 = state[H + 3] + ((ReadWord(block, 9) >> 6) & LimbMask);
        ulong h4 = state[H + 4] + ((ReadWord(block, 12) >> 8) | topBit);

        ulong d0 = (h0 * r0) + (h1 * s4) + (h2 * s3) + (h3 * s2) + (h4 * s1);
        ulong d1 = (h0 * r1) + (h1 * r0) + (h2 * s4) + (h3 * s3) + (h4 * s2);
        ulong d2 = (h0 * r2) + (h1 * r1) + (h2 * r0) + (h3 * s4) + (h4 * s3);
        ulong d3 = (h0 * r3) + (h1 * r2) + (h2 * r1) + (h3 * r0) + (h4 * s4);
        ulong d4 = (h0 * r4) + (h1 * r3) + (h2 * r2) + (h3 * r1) + (h4 * r0);

        ulong carry = d0 >> 26; state[H] = (uint)d0 & LimbMask;
        d1 += carry; carry = d1 >> 26; state[H + 1] = (uint)d1 & LimbMask;
        d2 += carry; carry = d2 >> 26; state[H + 2] = (uint)d2 & LimbMask;
        d3 += carry; carry = d3 >> 26; state[H + 3] = (uint)d3 & LimbMask;
        d4 += carry; carry = d4 >> 26; state[H + 4] = (uint)d4 & LimbMask;
        ulong low = state[H] + (carry * 5);
        state[H] = (uint)low & LimbMask;
        state[H + 1] += (uint)(low >> 26);
    }

    /// <summary>Returns <paramref name="whenSet" /> when <paramref name="mask" /> is all ones and <paramref name="whenClear" /> when it is zero, without a branch.</summary>
    private static uint SelectByMask(uint mask, uint whenSet, uint whenClear) =>
        (whenSet & mask) | (whenClear & ~mask);

    private static void RequireLength(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"Poly1305 needs {expected} bytes here; this is {length}.", parameterName);
        }
    }

    private static uint ReadWord(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
