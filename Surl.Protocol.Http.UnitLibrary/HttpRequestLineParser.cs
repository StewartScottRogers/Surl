using System.Text;

namespace Surl.Protocol.Http;

/// <summary>
/// Parses a request line, <c>method SP request-target SP HTTP-version</c> (RFC 9112,
/// section 3), accepting HTTP/1.0 and HTTP/1.1, and a higher HTTP/1.x as HTTP/1.1
/// (RFC 9110, section 2.5).
/// </summary>
internal static class HttpRequestLineParser
{
    private const int VersionLength = 8;

    /// <summary>
    /// Parses <paramref name="line"/>, its line ending already removed.
    /// </summary>
    /// <param name="line">The request line.</param>
    /// <param name="method">The method, when the line is well formed.</param>
    /// <param name="requestTarget">The raw request target, when the line is well formed.</param>
    /// <param name="version">The version, when the line is well formed.</param>
    /// <returns>
    /// <see langword="null"/> when the line is well formed and names an HTTP/1.x version;
    /// otherwise the failure.
    /// </returns>
    public static HttpRequestHeadReadOutcome? Parse(ReadOnlySpan<byte> line, out string method, out string requestTarget, out Version version)
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

        return ParseVersion(rest[(secondSpace + 1)..], out version);
    }

    private static HttpRequestHeadReadOutcome? ParseVersion(ReadOnlySpan<byte> bytes, out Version version)
    {
        version = new Version(0, 0);

        if (bytes.Length != VersionLength || !bytes.StartsWith("HTTP/"u8) || !IsDigitDotDigit(bytes[5..]))
        {
            return HttpRequestHeadReadOutcome.MalformedRequestLine;
        }

        var major = bytes[5] - '0';
        var minor = bytes[7] - '0';
        if (major != 1)
        {
            return HttpRequestHeadReadOutcome.UnsupportedVersion;
        }

        // RFC 9110, section 2.5: a higher 1.x minor version is treated as 1.1, the highest supported.
        version = new Version(1, Math.Min(minor, 1));

        return null;
    }

    private static bool IsDigitDotDigit(ReadOnlySpan<byte> bytes) =>
        char.IsAsciiDigit((char)bytes[0]) && bytes[1] == (byte)'.' && char.IsAsciiDigit((char)bytes[2]);
}
