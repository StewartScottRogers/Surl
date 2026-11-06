namespace Surl.Content;

/// <summary>
/// How <see cref="ContentStore.OpenUploadAsync(ContentPathMapping, ContentUploadOpening, CancellationToken)"/>
/// opens a random-access upload: what it starts from, and what it does about a file that is
/// there or missing.
/// </summary>
/// <remarks>
/// SFTP's <c>OPEN</c> with <c>WRITE</c> (ADR-0054 decision 9) reads its <c>pflags</c> into one:
/// <c>TRUNC</c> clears <paramref name="StartsFromExistingBytes"/>, <c>CREAT</c> sets
/// <paramref name="CreatesMissingFile"/>, and <c>CREAT</c> with <c>EXCL</c> sets
/// <paramref name="RefusesExistingFile"/>.
/// </remarks>
/// <param name="StartsFromExistingBytes">Whether the upload starts from a copy of the bytes of
/// the file already there; when no file is there, or when this is off, it starts empty.</param>
/// <param name="CreatesMissingFile">Whether the upload is opened when no file is there; when
/// off, nothing there is <see cref="ContentUploadOpeningResult.Absent"/>.</param>
/// <param name="RefusesExistingFile">Whether a file already there is
/// <see cref="ContentUploadOpeningResult.Exists"/>.</param>
public sealed record ContentUploadOpening(bool StartsFromExistingBytes, bool CreatesMissingFile, bool RefusesExistingFile);
