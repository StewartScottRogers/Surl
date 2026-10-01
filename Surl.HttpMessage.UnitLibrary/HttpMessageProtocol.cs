using System.Globalization;
using System.Text;

namespace Surl.HttpMessage;

/// <summary>
/// The protocol whose HTTP/1.x message syntax a request head is read in and a response head
/// written in: its name and the highest minor version of major version 1 the server speaks
/// (ADR-0070, decision 2).
/// </summary>
/// <remarks>
/// RTSP/1.0 borrows HTTP/1.1's syntax with its own name in the version (RFC 2326, sections 4,
/// 6 and 7), so one reader and one writer serve both, told which protocol they speak.
/// </remarks>
public sealed class HttpMessageProtocol
{
    private HttpMessageProtocol(string name, int highestMinorVersion)
    {
        Name = name;
        HighestMinorVersion = highestMinorVersion;
        HighestVersionText = string.Concat(name, "/1.", highestMinorVersion.ToString(CultureInfo.InvariantCulture));
        VersionPrefix = Encoding.ASCII.GetBytes(name + "/");
    }

    /// <summary>
    /// HTTP, up to HTTP/1.1: a request names <c>HTTP/1.0</c> or <c>HTTP/1.1</c>, and a
    /// response head's status line names <c>HTTP/1.1</c>.
    /// </summary>
    public static HttpMessageProtocol Http11 { get; } = new("HTTP", 1);

    /// <summary>
    /// RTSP/1.0: a request names <c>RTSP/1.0</c>, and a response head's status line names
    /// <c>RTSP/1.0</c>.
    /// </summary>
    public static HttpMessageProtocol Rtsp10 { get; } = new("RTSP", 0);

    /// <summary>
    /// The protocol name a version names before its slash: <c>HTTP</c> or <c>RTSP</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The highest minor version of major version 1 spoken; a request naming a higher one is
    /// read as this one (RFC 9110, section 2.5; RFC 2326, section 3.1).
    /// </summary>
    public int HighestMinorVersion { get; }

    /// <summary>
    /// The version a response head's status line names: <c>HTTP/1.1</c> or <c>RTSP/1.0</c>.
    /// </summary>
    public string HighestVersionText { get; }

    /// <summary>
    /// The ASCII bytes a request line's version starts with: the name and a slash.
    /// </summary>
    internal byte[] VersionPrefix { get; }
}
