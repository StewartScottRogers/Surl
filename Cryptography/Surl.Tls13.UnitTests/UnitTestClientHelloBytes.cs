namespace Surl.Tls13;

/// <summary>
/// Writes ClientHello bytes field by field, so a test can make exactly one field malformed. Each
/// default is a well-formed RFC 8446 section 4.1.2 value.
/// </summary>
internal sealed class UnitTestClientHelloBytes
{
    public byte HandshakeType { get; init; } = 1;

    public byte[] LegacyVersion { get; init; } = [0x03, 0x03];

    public byte[] SessionIdVector { get; init; } = [0];

    public byte[] CipherSuitesVector { get; init; } = [0x00, 0x02, 0x13, 0x01];

    public byte[] CompressionVector { get; init; } = [0x01, 0x00];

    /// <summary>Each extension as type, then data; the length prefixes are written for it.</summary>
    public (ushort Type, byte[] Data)[] Extensions { get; init; } = [(43, [0x02, 0x03, 0x04])];

    public bool OmitExtensions { get; init; }

    public byte[] Trailing { get; init; } = [];

    public byte[] Write()
    {
        var body = new List<byte>();
        body.AddRange(LegacyVersion);
        body.AddRange(new byte[32]);
        body.AddRange(SessionIdVector);
        body.AddRange(CipherSuitesVector);
        body.AddRange(CompressionVector);
        if (!OmitExtensions)
        {
            var block = new List<byte>();
            foreach (var (type, data) in Extensions)
            {
                block.AddRange([(byte)(type >> 8), (byte)type, (byte)(data.Length >> 8), (byte)data.Length]);
                block.AddRange(data);
            }

            body.AddRange([(byte)(block.Count >> 8), (byte)block.Count]);
            body.AddRange(block);
        }

        body.AddRange(Trailing);
        return [HandshakeType, (byte)(body.Count >> 16), (byte)(body.Count >> 8), (byte)body.Count, .. body];
    }
}
