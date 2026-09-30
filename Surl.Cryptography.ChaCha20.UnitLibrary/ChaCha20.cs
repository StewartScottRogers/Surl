using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Surl.Cryptography.ChaCha20;

/// <summary>
/// The ChaCha20 block function and stream cipher in two forms that share one block
/// function: RFC 8439's, with a <see cref="NonceSize" />-byte nonce and a 32-bit block
/// counter (sections 2.1 to 2.4), and the original form of Bernstein's ChaCha, with an
/// <see cref="OriginalNonceSize" />-byte nonce and a 64-bit block counter, which OpenSSH's
/// <c>chacha20-poly1305@openssh.com</c> uses (<c>PROTOCOL.chacha20poly1305</c>: the packet
/// sequence number as the nonce, block 0 for the Poly1305 key and block 1 onwards for the
/// payload).
/// </summary>
/// <remarks>
/// <para>
/// The block counter never wraps. A call whose last block would need a counter past the
/// form's width - past <see cref="uint.MaxValue" /> in the RFC 8439 form, past
/// <see cref="ulong.MaxValue" /> in the original form - throws
/// <see cref="ArgumentOutOfRangeException" /> before writing anything, because a wrapped
/// counter would reuse keystream. A block at the largest counter itself is allowed.
/// </para>
/// <para>
/// Constant-time: the block function is additions, rotations and exclusive-ors on fixed
/// state words, with no branch, index or loop bound that depends on the key or the data.
/// The state and keystream are zeroed before returning.
/// </para>
/// </remarks>
public static class ChaCha20
{
    /// <summary>The length in bytes of a key.</summary>
    public const int KeySize = 32;

    /// <summary>The length in bytes of RFC 8439's nonce, which leaves a 32-bit block counter.</summary>
    public const int NonceSize = 12;

    /// <summary>The length in bytes of the original ChaCha nonce, which leaves a 64-bit block counter.</summary>
    public const int OriginalNonceSize = 8;

    /// <summary>The length in bytes of one keystream block.</summary>
    public const int BlockSize = 64;

    private const int StateWords = 16;

    private const int CounterWord = 12;

    /// <summary>
    /// Computes the RFC 8439 block function for <paramref name="key" />,
    /// <paramref name="nonce" /> and block <paramref name="counter" /> into
    /// <paramref name="block" /> (RFC 8439 section 2.3).
    /// </summary>
    /// <param name="key">The <see cref="KeySize" />-byte key.</param>
    /// <param name="nonce">The <see cref="NonceSize" />-byte nonce.</param>
    /// <param name="counter">The 32-bit block counter.</param>
    /// <param name="block">Receives the <see cref="BlockSize" />-byte keystream block.</param>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static void ComputeBlock(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, uint counter, Span<byte> block)
    {
        RequireLength(nonce.Length, NonceSize, nameof(nonce));
        ComputeBlock(key, nonce, counter, 1, block);
    }

    /// <summary>
    /// Computes the original ChaCha20 block function, with a 64-bit block
    /// <paramref name="counter" /> beside an <see cref="OriginalNonceSize" />-byte
    /// <paramref name="nonce" />, into <paramref name="block" />: the form
    /// <c>chacha20-poly1305@openssh.com</c> takes its Poly1305 key from, at counter 0.
    /// </summary>
    /// <param name="key">The <see cref="KeySize" />-byte key.</param>
    /// <param name="nonce">The <see cref="OriginalNonceSize" />-byte nonce.</param>
    /// <param name="counter">The 64-bit block counter.</param>
    /// <param name="block">Receives the <see cref="BlockSize" />-byte keystream block.</param>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static void ComputeOriginalBlock(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ulong counter, Span<byte> block)
    {
        RequireLength(nonce.Length, OriginalNonceSize, nameof(nonce));
        ComputeBlock(key, nonce, counter, 2, block);
    }

    /// <summary>
    /// Exclusive-ors <paramref name="source" /> with the RFC 8439 keystream that starts at
    /// block <paramref name="initialCounter" /> and writes the result to
    /// <paramref name="destination" />: encryption and decryption alike (RFC 8439 section
    /// 2.4). <paramref name="destination" /> may be <paramref name="source" /> itself.
    /// </summary>
    /// <param name="key">The <see cref="KeySize" />-byte key.</param>
    /// <param name="nonce">The <see cref="NonceSize" />-byte nonce.</param>
    /// <param name="initialCounter">The 32-bit block counter of the first block.</param>
    /// <param name="source">The plaintext or ciphertext.</param>
    /// <param name="destination">Receives the result; as long as <paramref name="source" />.</param>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The last block would need a counter past <see cref="uint.MaxValue" />.
    /// </exception>
    public static void ApplyKeyStream(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        uint initialCounter,
        ReadOnlySpan<byte> source,
        Span<byte> destination)
    {
        RequireLength(nonce.Length, NonceSize, nameof(nonce));
        ApplyKeyStream(key, nonce, initialCounter, 1, source, destination);
    }

    /// <summary>
    /// Exclusive-ors <paramref name="source" /> with the original ChaCha20 keystream, 64-bit
    /// block counter beside an <see cref="OriginalNonceSize" />-byte nonce, that starts at
    /// block <paramref name="initialCounter" />, and writes the result to
    /// <paramref name="destination" />. <paramref name="destination" /> may be
    /// <paramref name="source" /> itself.
    /// </summary>
    /// <param name="key">The <see cref="KeySize" />-byte key.</param>
    /// <param name="nonce">The <see cref="OriginalNonceSize" />-byte nonce.</param>
    /// <param name="initialCounter">The 64-bit block counter of the first block.</param>
    /// <param name="source">The plaintext or ciphertext.</param>
    /// <param name="destination">Receives the result; as long as <paramref name="source" />.</param>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The last block would need a counter past <see cref="ulong.MaxValue" />.
    /// </exception>
    public static void ApplyOriginalKeyStream(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ulong initialCounter,
        ReadOnlySpan<byte> source,
        Span<byte> destination)
    {
        RequireLength(nonce.Length, OriginalNonceSize, nameof(nonce));
        ApplyKeyStream(key, nonce, initialCounter, 2, source, destination);
    }

    /// <summary>The ChaCha quarter round on four words (RFC 8439 section 2.1).</summary>
    internal static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
    {
        a += b;
        d = BitOperations.RotateLeft(d ^ a, 16);
        c += d;
        b = BitOperations.RotateLeft(b ^ c, 12);
        a += b;
        d = BitOperations.RotateLeft(d ^ a, 8);
        c += d;
        b = BitOperations.RotateLeft(b ^ c, 7);
    }

    /// <summary>
    /// QUARTERROUND(<paramref name="a" />, <paramref name="b" />, <paramref name="c" />,
    /// <paramref name="d" />) on the words of <paramref name="state" /> at those indices
    /// (RFC 8439 section 2.2).
    /// </summary>
    internal static void QuarterRound(Span<uint> state, int a, int b, int c, int d) =>
        QuarterRound(ref state[a], ref state[b], ref state[c], ref state[d]);

    /// <summary>One keystream block: the key stream applied to a block of zeros.</summary>
    private static void ComputeBlock(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, ulong counter, int counterWords, Span<byte> block)
    {
        RequireLength(block.Length, BlockSize, nameof(block));
        Span<byte> zeros = stackalloc byte[BlockSize];
        zeros.Clear();
        ApplyKeyStream(key, nonce, counter, counterWords, zeros, block);
    }

    /// <summary>
    /// The stream cipher both forms share: <paramref name="counterWords" /> is 1 for RFC
    /// 8439's 32-bit counter and 2 for the original 64-bit one, and the nonce fills the
    /// state words the counter leaves.
    /// </summary>
    private static void ApplyKeyStream(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ulong initialCounter,
        int counterWords,
        ReadOnlySpan<byte> source,
        Span<byte> destination)
    {
        RequireLength(key.Length, KeySize, nameof(key));
        RequireLength(destination.Length, source.Length, nameof(destination));
        RequireCounterRoom(counterWords, initialCounter, source.Length);
        Span<uint> state = stackalloc uint[2 * StateWords];
        Span<byte> keyStream = stackalloc byte[BlockSize];
        try
        {
            Initialize(state[..StateWords], key, nonce, initialCounter, counterWords);
            for (int offset = 0; offset < source.Length; offset += BlockSize)
            {
                ComputeKeyStreamBlock(state[..StateWords], state[StateWords..], keyStream);
                int count = Math.Min(BlockSize, source.Length - offset);
                for (int index = 0; index < count; index++)
                {
                    destination[offset + index] = (byte)(source[offset + index] ^ keyStream[index]);
                }

                IncrementCounter(state, counterWords);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(state));
            CryptographicOperations.ZeroMemory(keyStream);
        }
    }

    private static void RequireLength(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"ChaCha20 needs {expected} bytes here; this is {length}.", parameterName);
        }
    }

    /// <summary>Refuses a message whose last block would need a counter wider than the form's.</summary>
    private static void RequireCounterRoom(int counterWords, ulong initialCounter, int messageLength)
    {
        ulong largestCounter = counterWords == 1 ? uint.MaxValue : ulong.MaxValue;
        ulong lastBlockOffset = (ulong)Math.Max(0, messageLength - 1) / BlockSize;
        if (initialCounter > largestCounter - lastBlockOffset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialCounter),
                $"Block counter {initialCounter} leaves no room for {messageLength} bytes in a {32 * counterWords}-bit counter.");
        }
    }

    /// <summary>
    /// Sets up the state of RFC 8439 section 2.3: the four constants, the key, then the
    /// counter in <paramref name="counterWords" /> little-endian words, then the nonce.
    /// </summary>
    private static void Initialize(
        Span<uint> state,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ulong counter,
        int counterWords)
    {
        state[0] = 0x61707865;
        state[1] = 0x3320646e;
        state[2] = 0x79622d32;
        state[3] = 0x6b206574;
        for (int word = 0; word < 8; word++)
        {
            state[4 + word] = BinaryPrimitives.ReadUInt32LittleEndian(key[(word * sizeof(uint))..]);
        }

        state[CounterWord] = (uint)counter;
        state[CounterWord + 1] = (uint)(counter >> 32);
        for (int word = CounterWord + counterWords; word < StateWords; word++)
        {
            int offset = (word - CounterWord - counterWords) * sizeof(uint);
            state[word] = BinaryPrimitives.ReadUInt32LittleEndian(nonce[offset..]);
        }
    }

    /// <summary>
    /// Runs the 20 rounds on a copy of <paramref name="input" />, adds the input back and
    /// serializes the result little-endian into <paramref name="keyStream" /> (RFC 8439
    /// section 2.3).
    /// </summary>
    private static void ComputeKeyStreamBlock(ReadOnlySpan<uint> input, Span<uint> working, Span<byte> keyStream)
    {
        input.CopyTo(working);
        for (int doubleRound = 0; doubleRound < 10; doubleRound++)
        {
            QuarterRound(working, 0, 4, 8, 12);
            QuarterRound(working, 1, 5, 9, 13);
            QuarterRound(working, 2, 6, 10, 14);
            QuarterRound(working, 3, 7, 11, 15);
            QuarterRound(working, 0, 5, 10, 15);
            QuarterRound(working, 1, 6, 11, 12);
            QuarterRound(working, 2, 7, 8, 13);
            QuarterRound(working, 3, 4, 9, 14);
        }

        for (int word = 0; word < StateWords; word++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(keyStream[(word * sizeof(uint))..], working[word] + input[word]);
        }
    }

    /// <summary>
    /// Moves to the next block: the low counter word always, and the high one on a carry
    /// when the counter is 64 bits wide.
    /// </summary>
    private static void IncrementCounter(Span<uint> state, int counterWords)
    {
        state[CounterWord]++;
        if (counterWords == 2 && state[CounterWord] == 0)
        {
            state[CounterWord + 1]++;
        }
    }
}
