namespace Surl.Content;

/// <summary>
/// Whether <see cref="ContentStore.OpenUploadAsync(ContentPathMapping, ContentUploadOpening, CancellationToken)"/>
/// opened a random-access upload, and why not when it did not.
/// </summary>
/// <remarks>
/// Each member but <see cref="Opened"/> creates nothing. The SFTP server answers each with the
/// status ADR-0054 decision 9 gives <c>OPEN</c> with <c>WRITE</c>.
/// </remarks>
public enum ContentUploadOpeningResult
{
    /// <summary>
    /// The upload is open, in a temporary file beside the target.
    /// </summary>
    Opened = 0,

    /// <summary>
    /// Uploads are off, or the location is hidden by the exposure options, lies under
    /// <c>/.surl</c>, or was asked for with a trailing <c>/</c>.
    /// </summary>
    NotPermitted = 1,

    /// <summary>
    /// The directory the target belongs in does not exist.
    /// </summary>
    NoSuchDirectory = 2,

    /// <summary>
    /// A directory is at the location.
    /// </summary>
    IsADirectory = 3,

    /// <summary>
    /// A file is at the location and the opening refuses an existing file.
    /// </summary>
    Exists = 4,

    /// <summary>
    /// Nothing is at the location and the opening does not create a missing file.
    /// </summary>
    Absent = 5,
}
