using System.Buffers.Binary;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// SSH public key blobs (RFC 4253 section 6.6) built field by field, and the
/// <c>authorized_keys</c> lines that carry them. Only public keys: no test holds a private key.
/// </summary>
internal static class SshTestKeys
{
    public static byte[] Ed25519(byte fill) => Blob(Text("ssh-ed25519"), Filled(32, fill));

    public static byte[] Ecdsa(string curve, int coordinateLength) =>
        Blob(Text("ecdsa-sha2-" + curve), Text(curve), [0x04, .. Filled(2 * coordinateLength, 0x11)]);

    public static byte[] Rsa() => Blob(Text("ssh-rsa"), [0x01, 0x00, 0x01], Filled(257, 0x5A));

    public static byte[] Dss() => Blob(Text("ssh-dss"), Filled(129, 0x21), Filled(21, 0x22), Filled(128, 0x23), Filled(128, 0x24));

    public static byte[] Blob(params byte[][] fields)
    {
        var blob = new List<byte>();
        foreach (var field in fields)
        {
            var length = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)field.Length);
            blob.AddRange(length);
            blob.AddRange(field);
        }

        return [.. blob];
    }

    public static byte[] Text(string value) => Encoding.UTF8.GetBytes(value);

    public static string Line(string keyType, byte[] blob, string comment = "") =>
        $"{keyType} {Convert.ToBase64String(blob)}{(comment.Length == 0 ? string.Empty : " " + comment)}";

    private static byte[] Filled(int length, byte fill) => Enumerable.Repeat(fill, length).ToArray();
}
