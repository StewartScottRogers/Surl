namespace Surl.Hpack;

/// <summary>Turns the hex dumps RFC 7541 Appendix C prints, spaces and all, into bytes.</summary>
internal static class Rfc7541Hex
{
    public static byte[] Bytes(string hexDump) => Convert.FromHexString(hexDump.Replace(" ", string.Empty, StringComparison.Ordinal));

    public static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}
