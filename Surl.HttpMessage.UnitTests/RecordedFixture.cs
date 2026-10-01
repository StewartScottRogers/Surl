namespace Surl.HttpMessage;

/// <summary>
/// Reads the upstream curl request heads embedded by link from
/// <c>Surl.Protocol.Http.UnitTests/Fixtures/</c> (see its README).
/// </summary>
internal static class RecordedFixture
{
    public static byte[] ReadRequestBytes(string caseName) => ReadBytes(caseName, "request.bin");

    public static byte[] ReadBytes(string caseName, string fileName)
    {
        using var stream = typeof(RecordedFixture).Assembly.GetManifestResourceStream($"Fixtures/{caseName}/{fileName}")
            ?? throw new InvalidOperationException($"No embedded fixture Fixtures/{caseName}/{fileName}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    public static IEnumerable<ReadOnlyMemory<byte>> Whole(byte[] bytes) => [bytes];

    public static IEnumerable<ReadOnlyMemory<byte>> OneBytePerRead(byte[] bytes) =>
        Enumerable.Range(0, bytes.Length).Select(index => new ReadOnlyMemory<byte>(bytes, index, 1));
}
