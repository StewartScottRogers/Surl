using Surl.Cryptography.Rc4;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The RC4 stream cipher as SSH names it, offered only with <c>--allow-weak-ssh-algorithms</c>:
/// <c>arcfour</c> (RFC 4253, section 6.3) with a 128-bit key and nothing discarded, and
/// <c>arcfour128</c> (RFC 4345, section 4) with a 128-bit key and the first 1536 keystream bytes
/// discarded. One keystream runs across every packet of the direction; packets are padded to 8
/// bytes, as for any stream cipher. The keystream is BL-224's hand-built <see cref="Rc4"/>.
/// </summary>
/// <param name="key">The 16-byte key.</param>
/// <param name="discardedKeyStreamLength">0 for <c>arcfour</c>, 1536 for <c>arcfour128</c>.</param>
internal sealed class SshArcfour(byte[] key, int discardedKeyStreamLength) : ISshPacketCipher
{
    /// <summary>The key length of both ciphers: 128 bits.</summary>
    public const int KeyLength = 16;

    /// <summary>How many keystream bytes <c>arcfour128</c> discards (RFC 4345, section 4).</summary>
    public const int Arcfour128Discard = 1536;

    private readonly Rc4 keyStream = new(key, discardedKeyStreamLength);

    /// <inheritdoc/>
    public int BlockSize => 8;

    /// <inheritdoc/>
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext) => Apply(plaintext);

    /// <inheritdoc/>
    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext) => Apply(ciphertext);

    private byte[] Apply(ReadOnlySpan<byte> input)
    {
        var output = new byte[input.Length];
        keyStream.ApplyKeyStream(input, output);

        return output;
    }
}
