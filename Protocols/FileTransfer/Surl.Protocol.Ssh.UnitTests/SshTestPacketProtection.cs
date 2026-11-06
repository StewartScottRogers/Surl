using System.Buffers.Binary;
using System.Security.Cryptography;
using Surl.Cryptography.Cast128;
using Surl.Cryptography.Rc4;
using Surl.Cryptography.Ripemd160;
using static Surl.Protocol.Ssh.SshTestExchange;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The test client's own packet protection for one direction, written out from RFC 4253
/// section 6 (CBC, <c>arcfour</c>, HMAC-SHA1 and HMAC-MD5 and their <c>-96</c> forms), RFC 4344
/// section 4 (AES-CTR), RFC 4345 (<c>arcfour128</c>), RFC 6668 (HMAC-SHA2), RFC 2286 (HMAC-RIPEMD-160), OpenSSH's
/// <c>PROTOCOL</c> sections 1.6 and 1.7 (AES-GCM and encrypt-then-MAC),
/// <see cref="SshTestChaCha20Poly1305"/>, the BCL's block ciphers run one block at a time and
/// the hand-built <see cref="Rc4"/>, Blowfish, <see cref="Cast128"/> and <see cref="HmacRipemd160"/>,
/// each run one block or one message at a time here, so no test checks the server's packet protection against
/// the server's own code.
/// </summary>
internal sealed class SshTestPacketProtection
{
    private readonly SymmetricAlgorithm? blockCipher;
    private readonly bool chainsBlocks;
    private readonly Func<byte[], byte[]>? encryptBlock;
    private readonly Func<byte[], byte[]>? decryptBlock;
    private readonly bool macIsRipemd160;
    private readonly byte[] counter = new byte[16];
    private readonly Rc4? rc4;
    private readonly int blockSize = 16;
    private readonly AesGcm? aesGcm;
    private readonly byte[] nonce = new byte[12];
    private readonly byte[] macKey = [];
    private readonly HashAlgorithmName macHash;
    private readonly int macLength;
    private readonly bool encryptThenMac;
    private readonly SshTestChaCha20Poly1305? chaCha20Poly1305;
    private byte[] chainingBlock = [];

    /// <param name="deriveKey">Derives one key of the exchange by its letter and length.</param>
    public SshTestPacketProtection(string cipher, string mac, Func<char, int, byte[]> deriveKey, bool clientToServer)
    {
        var (ivLetter, keyLetter, macLetter) = clientToServer ? ('A', 'C', 'E') : ('B', 'D', 'F');
        if (cipher == "chacha20-poly1305@openssh.com")
        {
            chaCha20Poly1305 = new SshTestChaCha20Poly1305(deriveKey(keyLetter, 64));

            return;
        }

        if (cipher.EndsWith("-gcm@openssh.com", StringComparison.Ordinal))
        {
            aesGcm = new AesGcm(deriveKey(keyLetter, int.Parse(cipher[3..6], System.Globalization.CultureInfo.InvariantCulture) / 8), 16);
            nonce = deriveKey(ivLetter, 12);

            return;
        }

        if (cipher.StartsWith("arcfour", StringComparison.Ordinal))
        {
            // RFC 4253 section 6.3 and RFC 4345 section 4: a 128-bit key; arcfour128 discards 1536 bytes.
            rc4 = new Rc4(deriveKey(keyLetter, 16), cipher == "arcfour128" ? 1536 : 0);
            blockSize = 8;
        }
        else if (cipher is "blowfish-cbc" or "cast128-cbc")
        {
            // RFC 4253 section 6.3: Blowfish and CAST-128 with 128-bit keys, in CBC mode.
            (encryptBlock, decryptBlock) = BlockFunctions(cipher, deriveKey(keyLetter, 16));
            blockSize = 8;
            chainsBlocks = true;
            chainingBlock = deriveKey(ivLetter, blockSize);
        }
        else
        {
            var (algorithm, keyLength) = cipher switch
            {
                "3des-cbc" => ((SymmetricAlgorithm)TripleDES.Create(), 24),
                "rijndael-cbc@lysator.liu.se" => (Aes.Create(), 32),
                _ => (Aes.Create(), int.Parse(cipher[3..6], System.Globalization.CultureInfo.InvariantCulture) / 8),
            };
            blockCipher = algorithm;
            blockCipher.Key = deriveKey(keyLetter, keyLength);
            encryptBlock = block => algorithm.EncryptEcb(block, PaddingMode.None);
            decryptBlock = block => algorithm.DecryptEcb(block, PaddingMode.None);
            blockSize = blockCipher.BlockSize / 8;
            chainsBlocks = cipher.EndsWith("-cbc", StringComparison.Ordinal) || cipher.EndsWith("-cbc@lysator.liu.se", StringComparison.Ordinal);
            chainingBlock = deriveKey(ivLetter, blockSize);
            counter = chainingBlock.ToArray();
        }

        (macHash, var macKeyLength) = mac switch
        {
            _ when mac.StartsWith("hmac-sha2-512", StringComparison.Ordinal) => (HashAlgorithmName.SHA512, 64),
            _ when mac.StartsWith("hmac-sha2-256", StringComparison.Ordinal) => (HashAlgorithmName.SHA256, 32),
            _ when mac.StartsWith("hmac-sha1", StringComparison.Ordinal) => (HashAlgorithmName.SHA1, 20),
            _ when mac.StartsWith("hmac-ripemd160", StringComparison.Ordinal) => (default, 20),
            _ => (HashAlgorithmName.MD5, 16),
        };
        macIsRipemd160 = mac.StartsWith("hmac-ripemd160", StringComparison.Ordinal);
        macKey = deriveKey(macLetter, macKeyLength);
        macLength = mac.EndsWith("-96", StringComparison.Ordinal) ? 12 : macKeyLength;
        encryptThenMac = mac.EndsWith("-etm@openssh.com", StringComparison.Ordinal);
    }

    private bool LengthInClear => aesGcm is not null || encryptThenMac;

    private int TagLength => aesGcm is not null ? 16 : macLength;

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
        var padding = blockSize - (aligned % blockSize);
        padding += padding < 4 ? blockSize : 0;
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
            var sent = Concat(packet[..4], Encrypt(packet[4..]));

            return Concat(sent, Mac(sequenceNumber, sent));
        }

        return Concat(Encrypt(packet), Mac(sequenceNumber, packet));
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
            var first = Decrypt(await readExactly(blockSize));
            var rest = Decrypt(await readExactly((int)BinaryPrimitives.ReadUInt32BigEndian(first) + 4 - blockSize));
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

        return Decrypt(ciphertext);
    }

    private byte[] Mac(uint sequenceNumber, byte[] covered) =>
        (macIsRipemd160
            ? HmacRipemd160.HashData(macKey, Concat(UInt32(sequenceNumber), covered))
            : CryptographicOperations.HmacData(macHash, macKey, Concat(UInt32(sequenceNumber), covered)))[..macLength];

    private static (Func<byte[], byte[]> Encrypt, Func<byte[], byte[]> Decrypt) BlockFunctions(string cipher, byte[] key)
    {
        if (cipher == "blowfish-cbc")
        {
            var blowfish = new Surl.Cryptography.Blowfish.Blowfish(key);

            return (block => Transform(blowfish.EncryptBlock, block), block => Transform(blowfish.DecryptBlock, block));
        }

        var cast128 = new Cast128(key);

        return (block => Transform(cast128.EncryptBlock, block), block => Transform(cast128.DecryptBlock, block));
    }

    private static byte[] Transform(BlockTransform transform, byte[] block)
    {
        var output = new byte[block.Length];
        transform(block, output);

        return output;
    }

    private byte[] Encrypt(byte[] bytes) => rc4 is not null ? ApplyRc4(bytes) : chainsBlocks ? CbcEncrypt(bytes) : Ctr(bytes);

    private byte[] Decrypt(byte[] bytes) => rc4 is not null ? ApplyRc4(bytes) : chainsBlocks ? CbcDecrypt(bytes) : Ctr(bytes);

    private byte[] ApplyRc4(byte[] bytes)
    {
        var output = new byte[bytes.Length];
        rc4!.ApplyKeyStream(bytes, output);

        return output;
    }

    // RFC 4253 section 6.3 and SP 800-38A section 6.2: C_i = E(P_i xor C_i-1), chained across packets.
    private byte[] CbcEncrypt(byte[] bytes)
    {
        var output = new byte[bytes.Length];
        for (var block = 0; block < bytes.Length; block += blockSize)
        {
            var mixed = bytes.Skip(block).Take(blockSize).Select((value, index) => (byte)(value ^ chainingBlock[index])).ToArray();
            chainingBlock = encryptBlock!(mixed);
            chainingBlock.CopyTo(output, block);
        }

        return output;
    }

    // P_i = D(C_i) xor C_i-1.
    private byte[] CbcDecrypt(byte[] bytes)
    {
        var output = new byte[bytes.Length];
        for (var block = 0; block < bytes.Length; block += blockSize)
        {
            var ciphertext = bytes[block..(block + blockSize)];
            var decrypted = decryptBlock!(ciphertext);
            decrypted.Select((value, index) => (byte)(value ^ chainingBlock[index])).ToArray().CopyTo(output, block);
            chainingBlock = ciphertext;
        }

        return output;
    }

    private byte[] Ctr(byte[] bytes)
    {
        var keyStream = new byte[bytes.Length];
        for (var block = 0; block < bytes.Length; block += 16)
        {
            blockCipher!.EncryptEcb(counter, PaddingMode.None).CopyTo(keyStream, block);
            for (var index = 15; index >= 0 && ++counter[index] == 0; index--)
            {
            }
        }

        return [.. bytes.Select((value, index) => (byte)(value ^ keyStream[index]))];
    }

    private delegate void BlockTransform(ReadOnlySpan<byte> source, Span<byte> destination);

    private void CountInvocation()
    {
        var invocation = BinaryPrimitives.ReadUInt64BigEndian(nonce.AsSpan(4)) + 1;
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4), invocation);
    }
}
