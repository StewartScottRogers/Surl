using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Surl.Protocol.Ftp;

/// <summary>
/// Reads the address and port an active-mode command names: <c>EPRT |&lt;af&gt;|&lt;address&gt;|&lt;port&gt;|</c>
/// (RFC 2428, section 2) and <c>PORT h1,h2,h3,h4,p1,p2</c> (RFC 959, section 4.1.2). Whether
/// the target may be dialled is decided elsewhere (ADR-0052, decision 6).
/// </summary>
internal static class FtpActiveTargetParser
{
    /// <summary>The reply to an argument that does not parse.</summary>
    public const string SyntaxError = "501 Syntax error in arguments";

    /// <summary>The reply to an <c>EPRT</c> or <c>EPSV</c> naming an address family other than 1 or 2.</summary>
    public const string UnsupportedNetworkProtocol = "522 Network protocol not supported, use (1,2)";

    /// <summary>
    /// Reads an <c>EPRT</c> argument: a delimiter, the address family (1 for IPv4, 2 for IPv6),
    /// the address and the port, each followed by the delimiter.
    /// </summary>
    /// <param name="argument">The argument as sent.</param>
    /// <param name="refusal">The reply when the result is <see langword="null"/>.</param>
    /// <returns>The target, or <see langword="null"/> when the argument is refused.</returns>
    public static IPEndPoint? ParseExtended(byte[] argument, out string refusal)
    {
        refusal = SyntaxError;
        if (ExtendedFields(argument) is not { } fields)
        {
            return null;
        }

        if (AddressFamilyNumbered(fields[1]) is not { } addressFamily)
        {
            refusal = UnsupportedNetworkProtocol;
            return null;
        }

        return ParseAddressOf(fields[2], addressFamily) is { } address && TryParsePort(fields[3], out var port)
            ? new IPEndPoint(address, port)
            : null;
    }

    /// <summary>
    /// Reads a <c>PORT</c> argument: six decimal numbers from 0 to 255, separated by commas, the
    /// first four the IPv4 address and the last two the port's high and low bytes.
    /// </summary>
    /// <param name="argument">The argument as sent.</param>
    /// <returns>The target, or <see langword="null"/> when the argument does not parse (<see cref="SyntaxError"/>).</returns>
    public static IPEndPoint? ParsePort(byte[] argument)
    {
        var fields = Encoding.Latin1.GetString(argument).Split(',');
        if (fields.Length != 6)
        {
            return null;
        }

        var numbers = new byte[6];
        for (var index = 0; index < fields.Length; index++)
        {
            if (!byte.TryParse(fields[index], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[index]))
            {
                return null;
            }
        }

        return new IPEndPoint(new IPAddress(numbers.AsSpan(0, 4)), (numbers[4] * 256) + numbers[5]);
    }

    // The argument split at its first character, the delimiter: exactly three fields between
    // four delimiters, or null.
    private static string[]? ExtendedFields(byte[] argument)
    {
        var text = Encoding.Latin1.GetString(argument);
        var fields = text.Split(text[0]);

        return fields.Length == 5 && fields[0].Length == 0 && fields[4].Length == 0 ? fields : null;
    }

    private static AddressFamily? AddressFamilyNumbered(string family) => family switch
    {
        "1" => AddressFamily.InterNetwork,
        "2" => AddressFamily.InterNetworkV6,
        _ => null,
    };

    private static IPAddress? ParseAddressOf(string text, AddressFamily family) =>
        IPAddress.TryParse(text, out var address) && address.AddressFamily == family ? address : null;

    private static bool TryParsePort(string text, out int port) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port <= IPEndPoint.MaxPort;
}
