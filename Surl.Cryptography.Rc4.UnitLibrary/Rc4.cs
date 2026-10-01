namespace Surl.Cryptography.Rc4;

/// <summary>
/// The RC4 stream cipher: one keystream, set up from a key of <see cref="MinimumKeySize" /> to
/// <see cref="MaximumKeySize" /> bytes, that optionally discards its first bytes and then
/// continues across every call to <see cref="ApplyKeyStream" />, as an SSH connection runs one
/// keystream across every packet. With a 16-byte key and nothing discarded it is RFC 4253
/// section 6.3's <c>arcfour</c>; with 1536 bytes discarded it is RFC 4345 section 4's
/// <c>arcfour128</c> (16-byte key) and <c>arcfour256</c> (32-byte key).
/// </summary>
/// <remarks>
/// The base class library has no RC4, so it is built by hand (ADR-0051). RC4 is not
/// constant-time: its state lookups are indexed by key-dependent bytes. It exists only so
/// Surl can answer an upstream curl that still offers the <c>arcfour</c> ciphers.
/// </remarks>
public sealed class Rc4
{
    /// <summary>The length in bytes of the shortest key RC4 accepts.</summary>
    public const int MinimumKeySize = 1;

    /// <summary>The length in bytes of the longest key RC4 accepts.</summary>
    public const int MaximumKeySize = 256;

    private const int StateSize = 256;

    private readonly byte[] state = new byte[StateSize];

    private byte i;

    private byte j;

    /// <summary>
    /// Runs RC4's key schedule over <paramref name="key" />, then discards the first
    /// <paramref name="discardedKeyStreamLength" /> bytes of the keystream.
    /// </summary>
    /// <param name="key">The key, <see cref="MinimumKeySize" /> to <see cref="MaximumKeySize" /> bytes.</param>
    /// <param name="discardedKeyStreamLength">
    /// How many keystream bytes to throw away before the first call to
    /// <see cref="ApplyKeyStream" />: 0 for <c>arcfour</c>, 1536 for <c>arcfour128</c> and
    /// <c>arcfour256</c>.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="key" /> is empty or longer than <see cref="MaximumKeySize" /> bytes.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="discardedKeyStreamLength" /> is negative.</exception>
    public Rc4(ReadOnlySpan<byte> key, int discardedKeyStreamLength)
    {
        if (key.Length is < MinimumKeySize or > MaximumKeySize)
        {
            throw new ArgumentException(
                $"An RC4 key is {MinimumKeySize} to {MaximumKeySize} bytes, not {key.Length}.",
                nameof(key));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(discardedKeyStreamLength);

        ScheduleKey(key);
        DiscardKeyStream(discardedKeyStreamLength);
    }

    /// <summary>
    /// Exclusive-ors <paramref name="source" /> with the next keystream bytes and writes the
    /// result to <paramref name="destination" />: encryption and decryption alike. The
    /// keystream carries on from where the previous call stopped. <paramref name="destination" />
    /// may be <paramref name="source" /> itself.
    /// </summary>
    /// <param name="source">The plaintext or ciphertext.</param>
    /// <param name="destination">Receives the result; as long as <paramref name="source" />.</param>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not as long as <paramref name="source" />.</exception>
    public void ApplyKeyStream(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (destination.Length != source.Length)
        {
            throw new ArgumentException(
                $"The destination is {destination.Length} bytes but the source is {source.Length}.",
                nameof(destination));
        }

        for (var index = 0; index < source.Length; index++)
        {
            destination[index] = (byte)(source[index] ^ NextKeyStreamByte());
        }
    }

    private void ScheduleKey(ReadOnlySpan<byte> key)
    {
        for (var index = 0; index < StateSize; index++)
        {
            state[index] = (byte)index;
        }

        byte mixed = 0;

        for (var index = 0; index < StateSize; index++)
        {
            mixed = (byte)(mixed + state[index] + key[index % key.Length]);
            (state[index], state[mixed]) = (state[mixed], state[index]);
        }
    }

    private void DiscardKeyStream(int length)
    {
        for (var index = 0; index < length; index++)
        {
            NextKeyStreamByte();
        }
    }

    private byte NextKeyStreamByte()
    {
        i++;
        j += state[i];
        (state[i], state[j]) = (state[j], state[i]);

        return state[(byte)(state[i] + state[j])];
    }
}
