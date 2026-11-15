namespace Surl.Networking;

/// <summary>Builds ClientHello records for the tests: one cipher suite, an x25519 key share, and the versions the test names.</summary>
internal static class ClientHelloBytes
{
    /// <summary>The handshake message of a ClientHello offering <paramref name="versions"/>, or no supported_versions when null.</summary>
    public static byte[] Message(ushort[]? versions, ushort cipherSuite = 0x1301)
    {
        List<byte> extensions = [];
        if (versions is not null)
        {
            Extension(extensions, 43, [(byte)(versions.Length * 2), .. versions.SelectMany(version => new[] { (byte)(version >> 8), (byte)version })]);
        }

        Extension(extensions, 10, [0, 2, 0x00, 0x1d]);
        Extension(extensions, 13, [0, 2, 0x04, 0x03]);
        Extension(extensions, 51, [0, 36, 0x00, 0x1d, 0, 32, .. Enumerable.Repeat((byte)9, 32)]);
        byte[] body =
        [
            3, 3,
            .. Enumerable.Repeat((byte)1, 32),
            0,
            0, 2, (byte)(cipherSuite >> 8), (byte)cipherSuite,
            1, 0,
            (byte)(extensions.Count >> 8), (byte)extensions.Count, .. extensions,
        ];

        return [1, 0, (byte)(body.Length >> 8), (byte)body.Length, .. body];
    }

    /// <summary>The record layer's plaintext record around <paramref name="fragment"/>.</summary>
    public static byte[] Record(byte contentType, byte[] fragment) =>
        [contentType, 3, 1, (byte)(fragment.Length >> 8), (byte)fragment.Length, .. fragment];

    private static void Extension(List<byte> extensions, ushort type, byte[] data) =>
        extensions.AddRange([(byte)(type >> 8), (byte)type, (byte)(data.Length >> 8), (byte)data.Length, .. data]);
}
