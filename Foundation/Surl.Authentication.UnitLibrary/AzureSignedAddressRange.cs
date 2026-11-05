using System.Net;

namespace Surl.Authentication;

/// <summary>
/// A shared access signature's <c>sip</c> field: one address, or a range <c>&lt;low&gt;-&lt;high&gt;</c>
/// of one family, inclusive ("Create a service SAS", "Specify an IP address or IP range").
/// </summary>
internal static class AzureSignedAddressRange
{
    /// <summary>
    /// Whether <paramref name="signedAddresses"/> admits <paramref name="peer"/>: always when the
    /// field is absent, never when it does not read. An IPv4-mapped IPv6 peer is compared as IPv4.
    /// </summary>
    /// <param name="signedAddresses">The <c>sip</c> field, or <see langword="null"/>.</param>
    /// <param name="peer">The address the request came from.</param>
    /// <returns><see langword="true"/> when the peer is in the range.</returns>
    public static bool Admits(string? signedAddresses, IPAddress peer)
    {
        if (signedAddresses is null)
        {
            return true;
        }

        var dash = signedAddresses.IndexOf('-', StringComparison.Ordinal);
        var lowText = dash < 0 ? signedAddresses : signedAddresses[..dash];
        var highText = dash < 0 ? signedAddresses : signedAddresses[(dash + 1)..];
        var address = (peer.IsIPv4MappedToIPv6 ? peer.MapToIPv4() : peer).GetAddressBytes();

        return IPAddress.TryParse(lowText, out var low)
            && IPAddress.TryParse(highText, out var high)
            && IsBetween(address, low.GetAddressBytes(), high.GetAddressBytes());
    }

    // Big-endian byte order is address order; every address must be the same length.
    private static bool IsBetween(byte[] address, byte[] low, byte[] high) =>
        address.Length == low.Length
        && address.Length == high.Length
        && address.AsSpan().SequenceCompareTo(low) >= 0
        && address.AsSpan().SequenceCompareTo(high) <= 0;
}
