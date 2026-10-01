using System.Buffers.Binary;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Reads the bytes recorded from pinned upstream curl (<c>Fixtures/README.md</c>), embedded in
/// this assembly.
/// </summary>
internal static class RecordedFixture
{
    /// <summary>
    /// Every byte curl sent in the named case, in order.
    /// </summary>
    /// <param name="caseName">The fixture folder's name.</param>
    /// <returns>The bytes of <c>request.bin</c>.</returns>
    public static byte[] ReadRequestBytes(string caseName)
    {
        using var stream = typeof(RecordedFixture).Assembly.GetManifestResourceStream($"Fixtures/{caseName}/request.bin")
            ?? throw new InvalidOperationException($"No embedded fixture Fixtures/{caseName}/request.bin.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    /// <summary>
    /// The named case's request bytes cut into its whole <c>LDAPMessage</c>s, each a
    /// <c>SEQUENCE</c> in the short or long length form, and its SASL security-layer buffers,
    /// each a 4-byte big-endian length then that many bytes, kept whole.
    /// </summary>
    /// <param name="caseName">The fixture folder's name.</param>
    /// <returns>Each message and buffer curl sent, in order.</returns>
    public static IReadOnlyList<byte[]> ReadRequestMessages(string caseName)
    {
        var bytes = ReadRequestBytes(caseName);
        var messages = new List<byte[]>();
        for (var start = 0; start < bytes.Length;)
        {
            var end = bytes[start] == 0x30
                ? EndOfMessage(bytes, start)
                : start + 4 + BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(start));
            messages.Add(bytes[start..end]);
            start = end;
        }

        return messages;
    }

    private static int EndOfMessage(byte[] bytes, int start)
    {
        var (headerBytes, valueBytes) = LengthAt(bytes, start + 1);
        return start + 1 + headerBytes + valueBytes;
    }

    private static (int HeaderBytes, int ValueBytes) LengthAt(byte[] bytes, int index)
    {
        if (bytes[index] < 0x80)
        {
            return (1, bytes[index]);
        }

        var octets = bytes[index] & 0x7F;
        var length = 0;
        for (var octet = 1; octet <= octets; octet++)
        {
            length = (length << 8) | bytes[index + octet];
        }

        return (1 + octets, length);
    }
}
