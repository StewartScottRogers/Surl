using System.Text;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// What the server checks of an object name before the store sees it (ADR-0110, decision 2).
/// </summary>
internal static class GcsObjectName
{
    /// <summary>
    /// The most UTF-8 bytes an object name may have (the Cloud Storage object naming limit).
    /// </summary>
    public const int MaxNameBytes = 1024;

    /// <summary>
    /// Whether a name, percent-decoded once as UTF-8, is longer than <see cref="MaxNameBytes"/> bytes.
    /// </summary>
    /// <param name="rawName">The name as the client sent it, still percent-encoded.</param>
    /// <returns><see langword="true"/> when the name is too long.</returns>
    public static bool IsTooLong(string rawName) =>
        Encoding.UTF8.GetByteCount(Uri.UnescapeDataString(rawName)) > MaxNameBytes;
}
