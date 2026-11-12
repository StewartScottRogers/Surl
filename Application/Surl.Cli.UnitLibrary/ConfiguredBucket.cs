namespace Surl.Cli;

/// <summary>
/// One <c>--bucket &lt;name&gt;=&lt;path&gt;</c> (ADR-0101 decision 4, ADR-0102 decision 2.4): a bucket name the
/// object-storage servers map onto a content-store path. The record holds what was written, percent-decoded;
/// the path is a content-store path (<c>/media/photos</c>), not a path on disk.
/// </summary>
/// <param name="Name">The bucket's name, never empty, never holding a control character.</param>
/// <param name="Path">The content-store path as written, never empty, never holding U+0000.</param>
public sealed record ConfiguredBucket(string Name, string Path);
