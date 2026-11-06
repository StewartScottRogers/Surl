namespace Surl.Extended.Protocol.WebTransport;

/// <summary>
/// Maps a WebTransport application's 32-bit error code into the HTTP/3 error codes reserved for
/// it, <c>WT_APPLICATION_ERROR</c>, and back (draft-ietf-webtrans-http3-16 section 4.4). The range
/// skips the HTTP/3 codes of the form <c>0x1f * N + 0x21</c>, which RFC 9114 section 8.1 reserves
/// for greasing.
/// </summary>
public static class WebTransportApplicationErrorCode
{
    /// <summary>The HTTP/3 code application code 0 maps to.</summary>
    public const ulong FirstHttp3ErrorCode = 0x52e4a40fa8db;

    /// <summary>The HTTP/3 code application code <see cref="uint.MaxValue"/> maps to.</summary>
    public const ulong LastHttp3ErrorCode = 0x52e5ac983162;

    private const ulong GreaseStep = 0x1f;

    private const ulong GreaseOffset = 0x21;

    /// <summary>Returns the HTTP/3 error code that carries application code <paramref name="applicationErrorCode"/>.</summary>
    /// <param name="applicationErrorCode">The application's code.</param>
    /// <returns><c>0x52e4a40fa8db + n + floor(n / 0x1e)</c>.</returns>
    public static ulong ToHttp3ErrorCode(uint applicationErrorCode) =>
        FirstHttp3ErrorCode + applicationErrorCode + (applicationErrorCode / (GreaseStep - 1));

    /// <summary>Reads the application code an HTTP/3 error code carries.</summary>
    /// <param name="http3ErrorCode">The HTTP/3 error code received.</param>
    /// <param name="applicationErrorCode">The application's code, or 0 when there is none.</param>
    /// <returns><see langword="false"/> when the code is outside <c>WT_APPLICATION_ERROR</c>'s range or is a reserved greasing code within it.</returns>
    public static bool TryFromHttp3ErrorCode(ulong http3ErrorCode, out uint applicationErrorCode)
    {
        applicationErrorCode = 0;
        if (http3ErrorCode is < FirstHttp3ErrorCode or > LastHttp3ErrorCode
            || (http3ErrorCode - GreaseOffset) % GreaseStep == 0)
        {
            return false;
        }

        var shifted = http3ErrorCode - FirstHttp3ErrorCode;
        applicationErrorCode = (uint)(shifted - (shifted / GreaseStep));
        return true;
    }
}
