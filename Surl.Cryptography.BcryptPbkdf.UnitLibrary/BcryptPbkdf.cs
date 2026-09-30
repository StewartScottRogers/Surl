using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Surl.Cryptography.BcryptPbkdf;

/// <summary>
/// OpenBSD's bcrypt-pbkdf (<c>lib/libutil/bcrypt_pbkdf.c</c>, carried in openssh-portable
/// as <c>openbsd-compat/bcrypt_pbkdf.c</c>), the key derivation of
/// encrypted OpenSSH <c>openssh-key-v1</c> private keys (OpenSSH <c>PROTOCOL.key</c>, KDF
/// name <c>bcrypt</c>): PBKDF2's structure with SHA-512 as the pseudorandom function's
/// hash and bcrypt's expensive Blowfish key schedule as its core, the output bytes
/// interleaved across the blocks.
/// </summary>
/// <remarks>
/// Not constant-time: its core is Blowfish, whose S-box lookups depend on the password
/// by design. Every intermediate hash and the Blowfish state are zeroed before returning.
/// </remarks>
public static class BcryptPbkdf
{
    /// <summary>The longest key in bytes: 32 blocks of 32 bytes, OpenBSD's limit.</summary>
    public const int MaximumKeySize = HashSize * HashSize;

    /// <summary>The longest salt in bytes, OpenBSD's limit.</summary>
    public const int MaximumSaltSize = 1 << 20;

    /// <summary>The length in bytes of one bcrypt hash, and so of one derived block.</summary>
    internal const int HashSize = 32;

    private const int ExpensiveRounds = 64;

    /// <summary>
    /// Derives <paramref name="destination" />'s length of key bytes from
    /// <paramref name="password" /> and <paramref name="salt" /> with
    /// <paramref name="rounds" /> iterations (<c>bcrypt_pbkdf</c>).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="password" /> is empty, <paramref name="salt" /> is empty or longer
    /// than <see cref="MaximumSaltSize" />, <paramref name="destination" /> is empty or
    /// longer than <see cref="MaximumKeySize" />, or <paramref name="rounds" /> is below 1,
    /// all of which OpenBSD refuses.
    /// </exception>
    public static void DeriveKey(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, int rounds, Span<byte> destination)
    {
        RequireLengthWithin(password.Length, 1, int.MaxValue, nameof(password));
        RequireLengthWithin(salt.Length, 1, MaximumSaltSize, nameof(salt));
        RequireLengthWithin(destination.Length, 1, MaximumKeySize, nameof(destination));
        ArgumentOutOfRangeException.ThrowIfLessThan(rounds, 1);

        int stride = (destination.Length + HashSize - 1) / HashSize;
        byte[] countedSalt = new byte[salt.Length + 4];
        Span<byte> hashes = stackalloc byte[(2 * SHA512.HashSizeInBytes) + (2 * HashSize)];
        Span<byte> passwordHash = hashes[..SHA512.HashSizeInBytes];
        Span<byte> block = hashes[^HashSize..];
        Blowfish.Blowfish state = new();
        try
        {
            salt.CopyTo(countedSalt);
            SHA512.HashData(password, passwordHash);
            for (int blockIndex = 0; blockIndex < stride; blockIndex++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(countedSalt.AsSpan(salt.Length), (uint)blockIndex + 1);
                DeriveBlock(state, passwordHash, countedSalt, rounds, hashes[SHA512.HashSizeInBytes..^HashSize], block);
                for (int index = 0; (index * stride) + blockIndex < destination.Length; index++)
                {
                    destination[(index * stride) + blockIndex] = block[index];
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(countedSalt);
            CryptographicOperations.ZeroMemory(hashes);
            state.Clear();
        }
    }

    /// <summary>
    /// The bcrypt hash of <c>bcrypt_pbkdf.c</c>: Blowfish keyed by the salted expensive
    /// schedule over <paramref name="passwordHash" /> and <paramref name="saltHash" />
    /// (64 bytes each), then 64 encryptions of "OxychromaticBlowfishSwatDynamite", written
    /// to <paramref name="output" /> as little-endian words.
    /// </summary>
    internal static void ComputeHash(Blowfish.Blowfish state, ReadOnlySpan<byte> passwordHash, ReadOnlySpan<byte> saltHash, Span<byte> output)
    {
        Span<uint> words = stackalloc uint[HashSize / 4];
        try
        {
            state.Initialize();
            state.ExpandKey(saltHash, passwordHash);
            for (int round = 0; round < ExpensiveRounds; round++)
            {
                state.ExpandKey(saltHash);
                state.ExpandKey(passwordHash);
            }

            int position = 0;
            for (int index = 0; index < words.Length; index++)
            {
                words[index] = Blowfish.Blowfish.ReadWord("OxychromaticBlowfishSwatDynamite"u8, ref position);
            }

            for (int round = 0; round < ExpensiveRounds; round++)
            {
                for (int index = 0; index < words.Length; index += 2)
                {
                    state.Encrypt(ref words[index], ref words[index + 1]);
                }
            }

            for (int index = 0; index < words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(output[(4 * index)..], words[index]);
            }
        }
        finally
        {
            words.Clear();
        }
    }

    private static void RequireLengthWithin(int length, int minimum, int maximum, string parameterName)
    {
        if (length < minimum || length > maximum)
        {
            throw new ArgumentException($"bcrypt-pbkdf needs {minimum} to {maximum} bytes here; this is {length}.", parameterName);
        }
    }

    /// <summary>
    /// One output block: the bcrypt hash of the password hash and the counted salt's hash,
    /// then <paramref name="rounds" /> - 1 more, each salted with the SHA-512 of the last,
    /// all exclusive-ored into <paramref name="block" />. <paramref name="scratch" /> holds
    /// a salt hash and a bcrypt hash.
    /// </summary>
    private static void DeriveBlock(Blowfish.Blowfish state, ReadOnlySpan<byte> passwordHash, ReadOnlySpan<byte> countedSalt, int rounds, Span<byte> scratch, Span<byte> block)
    {
        Span<byte> saltHash = scratch[..SHA512.HashSizeInBytes];
        Span<byte> hash = scratch[SHA512.HashSizeInBytes..][..HashSize];
        SHA512.HashData(countedSalt, saltHash);
        ComputeHash(state, passwordHash, saltHash, hash);
        hash.CopyTo(block);
        for (int round = 1; round < rounds; round++)
        {
            SHA512.HashData(hash, saltHash);
            ComputeHash(state, passwordHash, saltHash, hash);
            for (int index = 0; index < HashSize; index++)
            {
                block[index] ^= hash[index];
            }
        }
    }
}
