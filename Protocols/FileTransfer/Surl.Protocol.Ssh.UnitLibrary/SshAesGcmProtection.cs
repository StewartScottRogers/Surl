using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// One direction's packets under <c>aes128-gcm@openssh.com</c> or <c>aes256-gcm@openssh.com</c>
/// (RFC 5647 as OpenSSH's <c>PROTOCOL</c> section 1.6 changes it): <c>packet_length</c> is sent
/// in the clear as the additional authenticated data, the rest of the packet is encrypted with
/// the BCL's <see cref="AesGcm"/>, and the 16-byte tag follows. The 12-byte nonce is the
/// derived IV, its last 8 bytes an invocation counter that goes up by one per packet. No MAC is
/// agreed beside it. A tag that does not verify is <c>DISCONNECT</c> 5.
/// </summary>
internal sealed class SshAesGcmProtection : SshPacketProtection
{
    /// <summary>
    /// The nonce's length, which is also the IV derived for it.
    /// </summary>
    public const int NonceLength = 12;

    private const int InvocationCounterOffset = 4;

    private const int AuthenticationTagLength = 16;

    private readonly AesGcm aesGcm;
    private readonly byte[] nonce;

    /// <summary>
    /// Creates the protection at its first packet.
    /// </summary>
    /// <param name="key">The key: 16 or 32 bytes.</param>
    /// <param name="initialNonce">The derived IV: <see cref="NonceLength"/> bytes.</param>
    public SshAesGcmProtection(byte[] key, byte[] initialNonce)
    {
        aesGcm = new AesGcm(key, AuthenticationTagLength);
        nonce = (byte[])initialNonce.Clone();
    }

    /// <inheritdoc/>
    public override int BlockSize => 16;

    /// <inheritdoc/>
    public override bool EncryptsLength => false;

    /// <inheritdoc/>
    public override bool AlignsLength => false;

    /// <inheritdoc/>
    public override int TagLength => AuthenticationTagLength;

    /// <summary>
    /// The key length of an AES-GCM cipher.
    /// </summary>
    /// <param name="cipher">The cipher's name.</param>
    /// <returns>16 or 32 for <c>aes128-gcm@openssh.com</c> and <c>aes256-gcm@openssh.com</c>; <see langword="null"/> for any other.</returns>
    public static int? KeyLengthFor(string cipher) => cipher switch
    {
        "aes128-gcm@openssh.com" => 16,
        "aes256-gcm@openssh.com" => 32,
        _ => null,
    };

    /// <inheritdoc/>
    public override byte[] OpenHead(uint sequenceNumber, byte[] head) => head;

    /// <inheritdoc/>
    public override byte[] OpenBody(uint sequenceNumber, byte[] plainHead, byte[] rest)
    {
        var plaintext = new byte[rest.Length - TagLength];
        try
        {
            aesGcm.Decrypt(nonce, rest.AsSpan(0, plaintext.Length), rest.AsSpan(plaintext.Length), plaintext, plainHead);
        }
        catch (AuthenticationTagMismatchException)
        {
            throw MacError(sequenceNumber);
        }

        CountInvocation();

        return plaintext;
    }

    /// <inheritdoc/>
    public override byte[] Seal(uint sequenceNumber, byte[] packet)
    {
        var sent = new byte[packet.Length + TagLength];
        packet.AsSpan(0, sizeof(uint)).CopyTo(sent);
        var ciphertext = sent.AsSpan(sizeof(uint), packet.Length - sizeof(uint));
        aesGcm.Encrypt(nonce, packet.AsSpan(sizeof(uint)), ciphertext, sent.AsSpan(packet.Length), packet.AsSpan(0, sizeof(uint)));
        CountInvocation();

        return sent;
    }

    private void CountInvocation()
    {
        var counter = nonce.AsSpan(InvocationCounterOffset);
        BinaryPrimitives.WriteUInt64BigEndian(counter, unchecked(BinaryPrimitives.ReadUInt64BigEndian(counter) + 1));
    }
}
