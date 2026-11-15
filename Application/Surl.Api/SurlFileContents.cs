namespace Surl.Api;

/// <summary>One file with its exact bytes, as <see cref="SurlServerFiles.ReadAsync"/> reads it.</summary>
/// <param name="Bytes">The file's bytes.</param>
/// <param name="ByteCount">Their count.</param>
/// <param name="ModifiedAt">When the file last changed.</param>
/// <param name="ContentType">The media type a request for it is answered with.</param>
public sealed record SurlFileContents(ReadOnlyMemory<byte> Bytes, long ByteCount, DateTimeOffset ModifiedAt, string ContentType);
