using System.Security.Cryptography;

namespace Surl.TlsSrp;

/// <summary>
/// Builds TLS 1.2 CBC record fragments by hand from RFC 5246 section 6.2.3 and RFC 7366 section 3,
/// independently of <see cref="TlsCbcRecordProtection"/>, so a test can pin its bytes and forge
/// records with a wrong MAC or wrong padding.
/// </summary>
internal static class UnitTestCbcRecords
{
    public const ushort Version = 0x0303;

    /// <summary><c>HMAC_SHA1(key, seq_num + type + version + length + content)</c>.</summary>
    public static byte[] Mac(byte[] macKey, ulong sequenceNumber, TlsContentType contentType, byte[] content)
    {
        byte[] input =
        [
            .. BitConverter.GetBytes(sequenceNumber).Reverse(),
            (byte)contentType,
            0x03,
            0x03,
            (byte)(content.Length >> 8),
            (byte)content.Length,
            .. content,
        ];
        return HMACSHA1.HashData(macKey, input);
    }

    /// <summary>Encrypts whole blocks in CBC mode with no padding of its own.</summary>
    public static byte[] Encrypt(TlsBulkCipher cipher, byte[] key, byte[] iv, byte[] plaintext)
    {
        using SymmetricAlgorithm algorithm = cipher == TlsBulkCipher.TripleDesEdeCbc ? TripleDES.Create() : Aes.Create();
        algorithm.Key = key;
        return algorithm.EncryptCbc(plaintext, iv, PaddingMode.None);
    }

    /// <summary>An IV, then the CBC encryption of <c>content + mac + padding</c>.</summary>
    public static byte[] MacThenEncrypt(TlsBulkCipher cipher, byte[] key, byte[] iv, byte[] content, byte[] mac, byte[] padding) =>
        [.. iv, .. Encrypt(cipher, key, iv, [.. content, .. mac, .. padding])];

    /// <summary>An IV, the CBC encryption of <c>content + padding</c>, then the MAC over both.</summary>
    public static byte[] EncryptThenMac(
        TlsBulkCipher cipher, byte[] key, byte[] macKey, ulong sequenceNumber, TlsContentType contentType, byte[] iv, byte[] content, byte[] padding)
    {
        byte[] body = [.. iv, .. Encrypt(cipher, key, iv, [.. content, .. padding])];
        return [.. body, .. Mac(macKey, sequenceNumber, contentType, body)];
    }

    /// <summary><paramref name="count"/> bytes, each holding <c>count - 1</c>: correct padding.</summary>
    public static byte[] Padding(int count) => Enumerable.Repeat((byte)(count - 1), count).ToArray();
}
