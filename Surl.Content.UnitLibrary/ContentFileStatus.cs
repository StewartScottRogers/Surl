namespace Surl.Content;

/// <summary>
/// The size and last modification time of a file in the served root.
/// </summary>
/// <param name="Length">The file's length in bytes.</param>
/// <param name="LastModifiedUtc">When the file was last written, in UTC.</param>
public sealed record ContentFileStatus(long Length, DateTimeOffset LastModifiedUtc);
