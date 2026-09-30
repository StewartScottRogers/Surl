using System.Buffers.Binary;
using System.Security.Cryptography;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The test client's own packet protection for one direction, written out from RFC 4253
/// section 6, RFC 4344 section 4 (AES-CTR), RFC 6668 (HMAC-SHA2), OpenSSH's <c>PROTOCOL</c>
/// sections 1.6 and 1.7 (AES-GCM and encrypt-then-MAC), <see cref="SshTestChaCha20Poly1305"/>
/// and the BCL's primitives, so no test checks the server's packet protection against the
/// server's own code.
/// </summary>
internal sealed class SshTestPacketProtection
{
    private readonly Aes? aes;
    private readonly byte[] counter = new byte[16];
    private readonly AesGcm? aesGcm;
    private readonly byte[] nonce = new byte[12];
    private readonly byte[] macKey = [];
    private readonly HashAlgorithmName macHash;
    private readonly bool encryptThenMac;
    private readonly SshTestChaCha20Poly1305? chaCha20Poly1305;

    /// <param name="deriveKey">Derives one key of the exchange by its letter and length.</param>
    public SshTestPacketProtection(string cipher, string mac, Func<char, int, byte[]> deriveKey, bool clientToServer)
    {
        var (ivLetter, keyLetter, macLetter) = clientToServer ? ('A', 'C', 'E') : ('B', 'D', 'F');
        if (cipher == "chacha20-poly1305@openssh.com")
        {
            chaCha20Poly1305 = new SshTestChaCha20Poly1305(deriveKey(keyLetter, 64));

            return;
        }

        var keyBits = int.Parse(cipher[3..6], System.Globalization.CultureInfo.InvariantCulture);
        if (cipher.EndsWith("-gcm@openssh.com", StringComparison.Ordinal))
        {
            aesGcm = new AesGcm(deriveKey(keyLetter, keyBits / 8), 16);
            nonce = deriveKey(ivLetter, 12);

            return;
        }

        aes = Aes.Create();
        aes.Key = deriveKey(keyLetter, keyBits / 8);
        counter = deriveKey(ivLetter, 16);
        macHash = mac.StartsWith("hmac-sha2-512", StringComparison.Ordinal) ? HashAlgorithmName.SHA512 : HashAlgorithmName.SHA256;
        macKey = deriveKey(macLetter, macHash == HashAlgorithmName.SHA512 ? 64 : 32);
        encryptThenMac = mac.EndsWith("-etm@openssh.com", StringComparison.Ordinal);
    }

    private bool LengthInClear => aesGcm is not null || encryptThenMac;

    private int TagLength => aesGcm is not null ? 16 : macKey.Length;

    /// <summary>
    /// Frames <paramref name="payload"/> with random padding and protects it as packet
    /// <paramref name="sequenceNumber"/>.
    /// </summary>
    public byte[] Seal(uint sequenceNumber, byte[] payload)
    {
        if (chaCha20Poly1305 is not null)
        {
            return chaCha20Poly1305.Seal(sequenceNumber, SshTestChaCha20Poly1305.Frame(payload));
        }

        var aligned = (LengthInClear ? 0 : 4) + 1 + payload.Length;
        var padding = 16 - (aligned % 16);
        padding += padding < 4 ? 16 : 0;
        var packet = Concat(UInt32((uint)(1 + payload.Length + padding)), [(byte)padding], payload, RandomNumberGenerator.GetBytes(padding));

        if (aesGcm is not null)
        {
            var ciphertext = new byte[packet.Length - 4];
            var tag = new byte[16];
            aesGcm.Encrypt(nonce, packet.AsSpan(4), ciphertext, tag, packet.AsSpan(0, 4));
            CountInvocation();

            return Concat(packet[..4], ciphertext, tag);
        }

        if (encryptThenMac)
        {
            var sent = Concat(packet[..4], Ctr(packet[4..]));

            return Concat(sent, Mac(sequenceNumber, sent));
        }

        return Concat(Ctr(packet), Mac(sequenceNumber, packet));
    }

    /// <summary>
    /// Reads and opens the server's packet <paramref name="sequenceNumber"/>, failing the test
    /// if its MAC or tag does not verify.
    /// </summary>
    /// <returns>The payload.</returns>
    public async Task<byte[]> OpenAsync(Func<int, Task<byte[]>> readExactly, uint sequenceNumber)
    {
        byte[] packet;
        if (chaCha20Poly1305 is not null)
        {
            var sentLength = await readExactly(4);
            var length = chaCha20Poly1305.OpenLength(sequenceNumber, sentLength);
            var rest = await readExactly((int)BinaryPrimitives.ReadUInt32BigEndian(length) + 16);
            packet = Concat(length, chaCha20Poly1305.OpenRest(sequenceNumber, sentLength, rest));
        }
        else if (LengthInClear)
        {
            var length = await readExactly(4);
            var rest = await readExactly((int)BinaryPrimitives.ReadUInt32BigEndian(length) + TagLength);
            packet = Concat(length, OpenRest(sequenceNumber, length, rest[..^TagLength], rest[^TagLength..]));
        }
        else
        {
            var first = Ctr(await readExactly(16));
            var rest = Ctr(await readExactly((int)BinaryPrimitives.ReadUInt32BigEndian(first) + 4 - 16));
            packet = Concat(first, rest);
            CollectionAssert.AreEqual(Mac(sequenceNumber, packet), await readExactly(TagLength), "The server's MAC does not verify.");
        }

        return packet[5..(packet.Length - packet[4])];
    }

    private byte[] OpenRest(uint sequenceNumber, byte[] length, byte[] ciphertext, byte[] tag)
    {
        if (aesGcm is not null)
        {
            var plaintext = new byte[ciphertext.Length];
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, length);
            CountInvocation();

            return plaintext;
        }

        CollectionAssert.AreEqual(Mac(sequenceNumber, Concat(length, ciphertext)), tag, "The server's MAC does not verify.");

        return Ctr(ciphertext);
    }

    private byte[] Mac(uint sequenceNumber, byte[] covered) =>
        CryptographicOperations.HmacData(macHash, macKey, Concat(UInt32(sequenceNumber), covered));

    private byte[] Ctr(byte[] bytes)
    {
        var keyStream = new byte[bytes.Length];
        for (var block = 0; block < bytes.Length; block += 16)
        {
            aes!.EncryptEcb(counter, PaddingMode.None).CopyTo(keyStream, block);
            for (var index = 15; index >= 0 && ++counter[index] == 0; index--)
            {
            }
        }

        return [.. bytes.Select((value, index) => (byte)(value ^ keyStream[index]))];
    }

    private void CountInvocation()
    {
        var invocation = BinaryPrimitives.ReadUInt64BigEndian(nonce.AsSpan(4)) + 1;
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4), invocation);
    }
}
