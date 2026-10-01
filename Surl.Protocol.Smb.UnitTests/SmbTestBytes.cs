namespace Surl.Protocol.Smb;

/// <summary>
/// Hand-built SMB version 1 bytes, laid out field by field from [MS-CIFS] section 2.2.3.1,
/// independent of the code under test.
/// </summary>
internal static class SmbTestBytes
{
    // TID 0x0102, PID 0x00BA:0xD71D (curl's made-up 0xbad71d), UID 0x0304, MID 0x0506.
    private const string IdentifiersHex = "0201" + "1DD7" + "0403" + "0605";

    /// <summary>Parses hexadecimal, ignoring spaces.</summary>
    /// <param name="hex">The hexadecimal digits.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    /// <summary>The 32-byte header of a request of <paramref name="command"/> as curl lays it out: flags 0x18, flags2 0x0041.</summary>
    /// <param name="command">The command code.</param>
    /// <returns>The header, in hexadecimal.</returns>
    public static string RequestHeaderHex(byte command) =>
        "FF534D42" + command.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) + "00000000" + "18" + "4100" + "BA00"
        + "0000000000000000" + "0000" + IdentifiersHex;

    /// <summary>The header of a response: flags 0x98 with the reply bit, the request's flags2 and identifiers.</summary>
    /// <param name="command">The command code.</param>
    /// <param name="statusHex">The status, little-endian, in hexadecimal.</param>
    /// <param name="identifiersHex">TID, PID low, UID and MID, in hexadecimal.</param>
    /// <returns>The header, in hexadecimal.</returns>
    public static string ResponseHeaderHex(byte command, string statusHex = "00000000", string identifiersHex = IdentifiersHex) =>
        "FF534D42" + command.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) + statusHex + "98" + "4100" + "BA00"
        + "0000000000000000" + "0000" + identifiersHex;

    /// <summary>A request message, without its NetBIOS header.</summary>
    /// <param name="command">The command code.</param>
    /// <param name="bodyHex">The word count, parameters, byte count and bytes, in hexadecimal.</param>
    /// <returns>The message.</returns>
    public static byte[] Request(byte command, string bodyHex) => Hex(RequestHeaderHex(command) + bodyHex);

    /// <summary>The header every request built here carries.</summary>
    public static SmbHeader RequestHeader(byte command) => new(command, 0, 0x18, 0x0041, 0x00BA, 0x0102, 0xD71D, 0x0304, 0x0506);

    /// <summary>ASCII text followed by NUL, in hexadecimal.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The hexadecimal.</returns>
    public static string TerminatedHex(string text) => Convert.ToHexString(System.Text.Encoding.ASCII.GetBytes(text)) + "00";
}
