using Surl.Content;

namespace Surl.HttpServing;

/// <summary>
/// One part of a <see cref="HttpAnswerBody.ContentFileParts"/> body: bytes sent before it, then a
/// range of the file.
/// </summary>
/// <param name="Preamble">The bytes sent before the range: a part's delimiter and fields.</param>
/// <param name="Range">Which of the file's bytes the part carries.</param>
public sealed record ContentFilePart(ReadOnlyMemory<byte> Preamble, ContentByteRange Range);
