using System.Text;

namespace Surl.Extended.Protocol.S3;

/// <summary>
/// The order ListObjectsV2 returns keys in: "UTF-8 binary order" (ListObjectsV2; ADR-0108, decision 6).
/// </summary>
internal sealed class S3KeyOrder : IComparer<string>
{
    /// <summary>
    /// The one instance.
    /// </summary>
    public static S3KeyOrder Instance { get; } = new();

    /// <summary>
    /// Compares two keys by their UTF-8 bytes.
    /// </summary>
    /// <param name="x">One key.</param>
    /// <param name="y">The other key.</param>
    /// <returns>Less than 0 when <paramref name="x"/> sorts first, 0 when equal, else more than 0.</returns>
    public int Compare(string? x, string? y) =>
        Encoding.UTF8.GetBytes(x ?? string.Empty).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(y ?? string.Empty));
}
