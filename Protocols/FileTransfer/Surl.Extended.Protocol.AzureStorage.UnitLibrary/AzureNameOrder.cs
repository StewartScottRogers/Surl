using System.Text;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The order List Blobs returns names in: ascending UTF-8 byte order (ADR-0109, decision 5.3).
/// </summary>
internal sealed class AzureNameOrder : IComparer<string>
{
    /// <summary>
    /// The one instance.
    /// </summary>
    public static AzureNameOrder Instance { get; } = new();

    /// <summary>
    /// Compares two names by their UTF-8 bytes.
    /// </summary>
    /// <param name="x">One name.</param>
    /// <param name="y">The other name.</param>
    /// <returns>Less than 0 when <paramref name="x"/> sorts first, 0 when equal, else more than 0.</returns>
    public int Compare(string? x, string? y) =>
        Encoding.UTF8.GetBytes(x ?? string.Empty).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(y ?? string.Empty));
}
