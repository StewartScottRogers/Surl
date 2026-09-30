namespace Surl.Protocol.Ftp;

/// <summary>
/// Reads the upstream curl recordings embedded from <c>Fixtures/</c> (see its README).
/// </summary>
internal static class RecordedFixture
{
    public static byte[] ReadRequestBytes(string caseName) => ReadBytes(caseName, "request.bin");

    /// <summary>
    /// The replies the recorder sent curl, in order: each <c>&lt; </c> line of
    /// <c>transcript.txt</c> without its prefix, ended by CRLF.
    /// </summary>
    public static string ReadServerReplies(string caseName) => string.Concat(
        System.Text.Encoding.ASCII.GetString(ReadBytes(caseName, "transcript.txt"))
            .Split("\r\n")
            .Where(line => line.StartsWith("< ", StringComparison.Ordinal))
            .Select(line => line[2..] + "\r\n"));

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
