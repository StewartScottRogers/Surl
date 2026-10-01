using System.Text;

namespace Surl.HttpMessage;

/// <summary>
/// Parses a request line, <c>method SP request-target SP version</c> (RFC 9112, section 3;
/// RFC 2326, section 6.1), accepting <c>&lt;name&gt;/1.&lt;digit&gt;</c> for the protocol it
/// is given: HTTP/1.0 and HTTP/1.1, and a higher HTTP/1.x as HTTP/1.1 (RFC 9110,
/// section 2.5); RTSP/1.0, and a higher RTSP/1.x as RTSP/1.0.
/// </summary>
internal static class HttpRequestLineParser
{
    // "1.x" after the protocol name and its slash.
    private const int VersionNumberLength = 3;

    /// <summary>
    /// Parses <paramref name="line"/>, its line ending already removed.
    /// </summary>
    /// <param name="line">The request line.</param>
    /// <param name="protocol">The protocol whose name the version must carry.</param>
    /// <param name="method">The method, when the line is well formed.</param>
    /// <param name="requestTarget">The raw request target, when the line is well formed.</param>
    /// <param name="version">The version, when the line is well formed.</param>
    /// <returns>
    /// <see langword="null"/> when the line is well formed and names version 1.x of
    /// <paramref name="protocol"/>; otherwise the failure: another protocol name is
    /// <see cref="HttpRequestHeadReadOutcome.MalformedRequestLine"/>, another major version
    /// <see cref="HttpRequestHeadReadOutcome.UnsupportedVersion"/>.
    /// </returns>
    public static HttpRequestHeadReadOutcome? Parse(ReadOnlySpan<byte> line, HttpMessageProtocol protocol, out string method, out string requestTarget, out Version version)
    {
        method = string.Empty;
        requestTarget = string.Empty;
        version = new Version(0, 0);

        var firstSpace = line.IndexOf((byte)' ');
        var rest = line[(firstSpace + 1)..];
        var secondSpace = rest.IndexOf((byte)' ');
        if (firstSpace < 0 || secondSpace < 0
            || !HttpSyntax.IsToken(line[..firstSpace])
            || !HttpSyntax.IsRequestTarget(rest[..secondSpace]))
        {
            return HttpRequestHeadReadOutcome.MalformedRequestLine;
        }

        method = Encoding.ASCII.GetString(line[..firstSpace]);
        requestTarget = Encoding.Latin1.GetString(rest[..secondSpace]);

        return ParseVersion(rest[(secondSpace + 1)..], protocol, out version);
    }

    private static HttpRequestHeadReadOutcome? ParseVersion(ReadOnlySpan<byte> bytes, HttpMessageProtocol protocol, out Version version)
    {
        version = new Version(0, 0);

        var prefixLength = protocol.VersionPrefix.Length;
        if (bytes.Length != prefixLength + VersionNumberLength || !bytes.StartsWith(protocol.VersionPrefix) || !IsDigitDotDigit(bytes[prefixLength..]))
        {
            return HttpRequestHeadReadOutcome.MalformedRequestLine;
        }

        var major = bytes[prefixLength] - '0';
        var minor = bytes[prefixLength + 2] - '0';
        if (major != 1)
        {
            return HttpRequestHeadReadOutcome.UnsupportedVersion;
        }

        // RFC 9110, section 2.5: a higher 1.x minor version is treated as the highest supported.
        version = new Version(1, Math.Min(minor, protocol.HighestMinorVersion));

        return null;
    }

    private static bool IsDigitDotDigit(ReadOnlySpan<byte> bytes) =>
        char.IsAsciiDigit((char)bytes[0]) && bytes[1] == (byte)'.' && char.IsAsciiDigit((char)bytes[2]);
}
