namespace Surl.Content;

/// <summary>
/// What <see cref="ContentStore.OpenUploadAsync(ContentPathMapping, ContentUploadOpening, CancellationToken)"/>
/// answered: its result and, when the upload was opened, the session that writes it.
/// </summary>
/// <param name="Result">Whether the upload was opened, and why not when it was not.</param>
/// <param name="Session">The open upload when <paramref name="Result"/> is
/// <see cref="ContentUploadOpeningResult.Opened"/>, which the caller disposes;
/// <see langword="null"/> otherwise.</param>
public sealed record ContentUploadOpeningOutcome(ContentUploadOpeningResult Result, ContentUploadSession? Session);
