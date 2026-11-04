using System.Buffers.Binary;
using System.Text;

namespace Surl.Console;

/// <summary>
/// Hand-written MIT keytab files (version <c>0x0502</c>, ADR-0057 decision 1) for the
/// <c>--keytab</c> tests, with fixed keys (ADR-0057 decision 11).
/// </summary>
internal static class TestKeytabFiles
{
    /// <summary>The <c>aes128-cts-hmac-sha1-96</c> enctype number.</summary>
    public const int Aes128CtsHmacSha196 = 17;

    /// <summary>The <c>rc4-hmac</c> enctype number.</summary>
    public const int Rc4Hmac = 23;

    /// <summary>The principal every entry here is for.</summary>
    public const string Principal = "HTTP/www.example.com@EXAMPLE.COM";

    /// <summary>The 16-byte AES key: a run no other byte in the file makes.</summary>
    public static readonly byte[] AesKey =
        [0xA1, 0xB2, 0xC3, 0xD4, 0xE5, 0xF6, 0x07, 0x18, 0x29, 0x3A, 0x4B, 0x5C, 0x6D, 0x7E, 0x8F, 0x90];

    /// <summary>A 16-byte <c>rc4-hmac</c> key.</summary>
    public static readonly byte[] Rc4Key =
        [0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x01];

    /// <summary>A keytab of one <c>aes128-cts-hmac-sha1-96</c> key for <see cref="Principal"/>.</summary>
    public static byte[] AesOnly => Keytab(Entry(Aes128CtsHmacSha196, AesKey));

    /// <summary>A keytab of one <c>rc4-hmac</c> key, then one AES key, for <see cref="Principal"/>.</summary>
    public static byte[] Rc4ThenAes => Keytab(Entry(Rc4Hmac, Rc4Key), Entry(Aes128CtsHmacSha196, AesKey));

    /// <summary>A keytab of one <c>rc4-hmac</c> key for <see cref="Principal"/>.</summary>
    public static byte[] Rc4Only => Keytab(Entry(Rc4Hmac, Rc4Key));

    /// <summary>The version <c>0x05 0x02</c>, then each entry.</summary>
    /// <param name="entries">The length-prefixed entries.</param>
    /// <returns>The file's bytes.</returns>
    public static byte[] Keytab(params byte[][] entries) => [0x05, 0x02, .. entries.SelectMany(entry => entry)];

    /// <summary>
    /// One length-prefixed entry for <see cref="Principal"/>: two components, the realm, the name
    /// type 1, timestamp 0, key version number 1, the enctype and the key.
    /// </summary>
    /// <param name="encryptionType">The enctype number.</param>
    /// <param name="key">The key.</param>
    /// <returns>The entry's bytes, its 32-bit length first.</returns>
    public static byte[] Entry(int encryptionType, byte[] key)
    {
        List<byte> body = [];
        body.AddRange(UInt16(2));
        body.AddRange(Counted(Encoding.ASCII.GetBytes("EXAMPLE.COM")));
        body.AddRange(Counted(Encoding.ASCII.GetBytes("HTTP")));
        body.AddRange(Counted(Encoding.ASCII.GetBytes("www.example.com")));
        body.AddRange(UInt32(1));
        body.AddRange(UInt32(0));
        body.Add(1);
        body.AddRange(UInt16(encryptionType));
        body.AddRange(Counted(key));
        return [.. UInt32(body.Count), .. body];
    }

    private static byte[] Counted(byte[] bytes) => [.. UInt16(bytes.Length), .. bytes];

    private static byte[] UInt16(int value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)value);
        return bytes;
    }

    private static byte[] UInt32(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)value);
        return bytes;
    }
}
