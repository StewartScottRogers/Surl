using System.Text;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// The fixed <c>message</c> texts of ADR-0118 decision 6 this library answers with, and the ERROR
/// frames that carry them. No text names an account, a destination, a path or a version.
/// </summary>
internal static class StompErrors
{
    public const string UnsupportedProtocolVersion = "Unsupported protocol version";
    public const string AuthenticationFailed = "Authentication failed";
    public const string NotConnected = "Not connected";
    public const string AlreadyConnected = "Already connected";
    public const string UnknownCommand = "Unknown command";
    public const string MalformedFrame = "Malformed frame";
    public const string InvalidHeartBeat = "Invalid heart-beat";
    public const string HeaderLineTooLong = "Header line too long";
    public const string TooManyHeaders = "Too many headers";
    public const string FrameTooLarge = "Frame too large";
    public const string TooManyConnections = "Too many connections";

    private const string SupportedVersionsBody = "Supported protocol versions are 1.0 1.1 1.2";

    /// <summary>
    /// ERROR with <paramref name="message"/>, <c>receipt-id</c> when the frame at fault carried
    /// <c>receipt</c>, and no body.
    /// </summary>
    /// <param name="message">One of this class's texts.</param>
    /// <param name="receipt">The <c>receipt</c> of the frame at fault, or <see langword="null"/>.</param>
    public static StompFrame Frame(string message, string? receipt = null) =>
        receipt is null
            ? new StompFrame("ERROR", Header("message", message))
            : new StompFrame("ERROR", Header("message", message), Header("receipt-id", receipt));

    /// <summary>
    /// The ERROR that answers a CONNECT naming no version the server speaks (ADR-0118 decision 1),
    /// after the specification's own example.
    /// </summary>
    public static StompFrame UnsupportedVersionFrame()
    {
        var body = Encoding.UTF8.GetBytes(SupportedVersionsBody);
        return new StompFrame(
            "ERROR",
            [
                Header("message", UnsupportedProtocolVersion),
                Header("version", StompVersionNegotiation.SupportedVersions),
                Header("content-type", "text/plain"),
                Header("content-length", body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ],
            body);
    }

    private static KeyValuePair<string, string> Header(string name, string value) => new(name, value);
}
