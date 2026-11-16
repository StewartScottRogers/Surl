namespace Surl.Composition;

/// <summary>One stored file with its exact bytes, as a test reads it back.</summary>
/// <param name="Bytes">A copy of the file as stored.</param>
/// <param name="ByteCount">The file's size in bytes.</param>
/// <param name="ModifiedAt">When the file was last written, in UTC.</param>
/// <param name="ContentType">The media type its extension names; <c>application/octet-stream</c> when it names none.</param>
public sealed record ComposedFileContents(ReadOnlyMemory<byte> Bytes, long ByteCount, DateTimeOffset ModifiedAt, string ContentType);
