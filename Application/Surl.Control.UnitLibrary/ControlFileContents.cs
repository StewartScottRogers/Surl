namespace Surl.Control;

/// <summary>One file of the content store, read back.</summary>
/// <param name="Bytes">The file's bytes.</param>
/// <param name="ByteCount">Their count.</param>
/// <param name="ModifiedAt">When the file last changed.</param>
/// <param name="ContentType">The media type the HTTP server would give it.</param>
public sealed record ControlFileContents(ReadOnlyMemory<byte> Bytes, long ByteCount, DateTimeOffset ModifiedAt, string ContentType);
