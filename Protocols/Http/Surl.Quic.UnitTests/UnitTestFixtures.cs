namespace Surl.Quic;

/// <summary>
/// Reads the recorded client Initial datagrams embedded in this assembly from
/// Fixtures/ClientInitial. Each is the datagrams.bin that Record-CurlExchange.ps1 -QuicInitial
/// (BL-363) recorded from a pinned upstream curl on 2026-11-04 - every datagram prefixed with its
/// two-byte big-endian length; BL-377's Notes say how each was made.
/// </summary>
internal static class UnitTestFixtures
{
    /// <summary>The fixture of each pinned build: static 8.21.0 on Windows and Linux, and curl.se 8.22.0_2 on Windows.</summary>
    public static IEnumerable<object[]> RecordedBuilds =>
    [
        ["static-8.21.0-win.datagrams.bin"],
        ["static-8.21.0-linux.datagrams.bin"],
        ["curlse-8.22.0_2-win.datagrams.bin"],
    ];

    public static List<byte[]> ReadDatagrams(string name)
    {
        using var stream = typeof(UnitTestFixtures).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"No embedded fixture {name}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        var bytes = copy.ToArray();
        var datagrams = new List<byte[]>();
        for (var offset = 0; offset < bytes.Length;)
        {
            var length = (bytes[offset] << 8) | bytes[offset + 1];
            datagrams.Add(bytes[(offset + 2)..(offset + 2 + length)]);
            offset += 2 + length;
        }

        return datagrams;
    }
}
