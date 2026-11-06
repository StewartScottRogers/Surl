namespace Surl.Content;

/// <summary>
/// What a <c>--share-access</c> grant lets its member do in a share (ADR-0102 decision 2.2).
/// </summary>
public enum ContentShareAccess
{
    /// <summary><c>r</c>: read files and list folders.</summary>
    Read = 1,

    /// <summary>
    /// <c>rw</c>: also upload, append, rename, delete and create or remove folders - each still
    /// needing <c>--allow-uploads</c> and a share that is not <c>readonly</c>.
    /// </summary>
    ReadWrite = 2,
}
