namespace Surl.Qpack;

/// <summary>Turns the hex dumps RFC 9204 Appendix B prints, spaces and all, into bytes.</summary>
internal static class Rfc9204Hex
{
    public static byte[] Bytes(string hexDump) => Convert.FromHexString(hexDump.Replace(" ", string.Empty, StringComparison.Ordinal));

    public static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}
