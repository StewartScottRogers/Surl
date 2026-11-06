namespace Surl.Compression;

/// <summary>Inputs the encoder tests compress: repetitive, incompressible, and both at once.</summary>
internal static class CompressionTestInputs
{
    /// <summary>Text repeated until it is <paramref name="length"/> bytes long.</summary>
    public static byte[] Repetitive(int length) =>
        Enumerable.Range(0, length).Select(index => (byte)"the quick brown fox jumps over the lazy dog. "[index % 45]).ToArray();

    /// <summary><paramref name="length"/> bytes from a seeded generator, which no matcher shrinks.</summary>
    public static byte[] Incompressible(int length, int seed)
    {
        byte[] bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    /// <summary>
    /// Incompressible bytes, zeros, then the same bytes again, so the second copy lies more than
    /// <paramref name="distance"/> bytes after the first.
    /// </summary>
    public static byte[] RepeatedAfter(int distance)
    {
        byte[] head = Incompressible(1000, 7);
        return [.. head, .. new byte[distance], .. head];
    }
}
