namespace Surl.Http2;

/// <summary>
/// Reads the upstream curl recordings embedded from <c>Surl.Protocol.Http.UnitTests</c>'s
/// <c>Fixtures/</c> (see its README): the frames pinned upstream curl sent, which the codec reads.
/// </summary>
internal static class Http2RecordedFixture
{
    public static byte[] ReadRequestBytes(string caseName)
    {
        using var stream = typeof(Http2RecordedFixture).Assembly.GetManifestResourceStream($"Fixtures/{caseName}/request.bin")
            ?? throw new InvalidOperationException($"No embedded fixture Fixtures/{caseName}/request.bin.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    public static IEnumerable<ReadOnlyMemory<byte>> Whole(byte[] bytes) => [bytes];

    public static IEnumerable<ReadOnlyMemory<byte>> OneBytePerRead(byte[] bytes) =>
        Enumerable.Range(0, bytes.Length).Select(index => new ReadOnlyMemory<byte>(bytes, index, 1));
}
