namespace Surl.Content;

/// <summary>
/// What became of an upload <see cref="ContentStore.WriteUploadAsync(ContentPathMapping, Stream, CancellationToken)"/>
/// was given.
/// </summary>
public enum ContentUploadResult
{
    /// <summary>
    /// Every byte was written to the file at the mapped location.
    /// </summary>
    Written = 0,

    /// <summary>
    /// Nothing was written: uploads are off, or the location is hidden, a directory, or not
    /// inside an existing directory. Each protocol answers it with its own "not permitted"
    /// (HTTP 405 with <c>Allow</c>, FTP 550, TFTP error 2).
    /// </summary>
    NotPermitted = 1,

    /// <summary>
    /// The upload grew past <see cref="ContentExposureOptions.MaxUploadBytes"/>: reading
    /// stopped and the partial file was deleted.
    /// </summary>
    TooLarge = 2,
}
