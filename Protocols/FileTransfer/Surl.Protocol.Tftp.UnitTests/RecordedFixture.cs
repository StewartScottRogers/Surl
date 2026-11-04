using System.Globalization;

namespace Surl.Protocol.Tftp;

/// <summary>
/// Reads the upstream curl recordings embedded from <c>Fixtures/</c> (see its README).
/// </summary>
internal static class RecordedFixture
{
    public static byte[] ReadBytes(string caseName, string fileName)
    {
        using var stream = typeof(RecordedFixture).Assembly.GetManifestResourceStream($"Fixtures/{caseName}/{fileName}")
            ?? throw new InvalidOperationException($"No embedded fixture Fixtures/{caseName}/{fileName}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    /// <summary>
    /// The datagrams of <c>transcript.txt</c>, in order: whether curl sent each one, and its
    /// bytes, with the recorder's <c>\xHH</c> and <c>\\</c> escapes undone.
    /// </summary>
    public static IReadOnlyList<(bool FromCurl, byte[] Bytes)> ReadDatagrams(string caseName)
    {
        var text = System.Text.Encoding.Latin1.GetString(ReadBytes(caseName, "transcript.txt"));
        return text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(line => (line.StartsWith('>'), Unescape(line[(line.IndexOf(' ', line.IndexOf("-> ", StringComparison.Ordinal) + 3) + 1)..])))
            .ToList();
    }

    public static IReadOnlyList<byte[]> CurlDatagrams(string caseName) =>
        ReadDatagrams(caseName).Where(datagram => datagram.FromCurl).Select(datagram => datagram.Bytes).ToList();

    public static IReadOnlyList<byte[]> ServerDatagrams(string caseName) =>
        ReadDatagrams(caseName).Where(datagram => !datagram.FromCurl).Select(datagram => datagram.Bytes).ToList();

    private static byte[] Unescape(string escaped)
    {
        var bytes = new List<byte>();
        for (var index = 0; index < escaped.Length; index++)
        {
            if (escaped[index] != '\\')
            {
                bytes.Add((byte)escaped[index]);
            }
            else if (escaped[index + 1] == '\\')
            {
                bytes.Add((byte)'\\');
                index++;
            }
            else
            {
                bytes.Add(byte.Parse(escaped.AsSpan(index + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                index += 3;
            }
        }

        return [.. bytes];
    }
}
