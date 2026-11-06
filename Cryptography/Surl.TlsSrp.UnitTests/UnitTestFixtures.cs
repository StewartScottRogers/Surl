namespace Surl.TlsSrp;

/// <summary>Reads the embedded bytes recorded from pinned upstream curl (Fixtures/README.md).</summary>
internal static class UnitTestFixtures
{
    public static byte[] Read(string caseName, string fileName)
    {
        using var resource = typeof(UnitTestFixtures).Assembly.GetManifestResourceStream($"Fixtures/{caseName}/{fileName}")
            ?? throw new InvalidOperationException($"No fixture {caseName}/{fileName}.");
        using var copy = new MemoryStream();
        resource.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>Splits a capture into its records' fragments, header excluded, in order.</summary>
    public static List<byte[]> Fragments(byte[] capture)
    {
        var fragments = new List<byte[]>();
        for (var offset = 0; offset < capture.Length;)
        {
            var length = (capture[offset + 3] << 8) | capture[offset + 4];
            fragments.Add(capture[(offset + TlsRecordFormat.HeaderLength)..(offset + TlsRecordFormat.HeaderLength + length)]);
            offset += TlsRecordFormat.HeaderLength + length;
        }

        return fragments;
    }
}
