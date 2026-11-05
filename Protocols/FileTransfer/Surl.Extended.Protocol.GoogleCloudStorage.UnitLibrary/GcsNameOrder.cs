using System.Text;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// The order List Objects returns names in: lexicographic, by UTF-8 bytes ("GET Bucket (List Objects)"; ADR-0110, decision 6).
/// </summary>
internal sealed class GcsNameOrder : IComparer<string>
{
    /// <summary>
    /// The one instance.
    /// </summary>
    public static GcsNameOrder Instance { get; } = new();

    /// <summary>
    /// Compares two keys by their UTF-8 bytes.
    /// </summary>
    /// <param name="x">One key.</param>
    /// <param name="y">The other key.</param>
    /// <returns>Less than 0 when <paramref name="x"/> sorts first, 0 when equal, else more than 0.</returns>
    public int Compare(string? x, string? y) =>
        Encoding.UTF8.GetBytes(x ?? string.Empty).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(y ?? string.Empty));
}
