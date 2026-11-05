using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// Checks the client's <c>SETTINGS</c> payload (RFC 9114 section 7.2.4). The server needs none of
/// its values - its encoder never uses a dynamic table and its answers are small - so it only
/// enforces the rules.
/// </summary>
internal static class Http3Settings
{
    /// <summary>
    /// Throws when the payload is not whole identifier and value pairs (<c>H3_FRAME_ERROR</c>), or
    /// repeats an identifier or carries one of HTTP/2's, 0x02 to 0x05 (<c>H3_SETTINGS_ERROR</c>).
    /// </summary>
    /// <param name="payload">The <c>SETTINGS</c> frame's payload.</param>
    /// <exception cref="Http3ConnectionException">The payload breaks a rule.</exception>
    public static void Validate(ReadOnlySpan<byte> payload)
    {
        var seen = new HashSet<ulong>();
        while (!payload.IsEmpty)
        {
            var (identifier, length) = ReadSetting(payload);
            if (IsHttp2Setting(identifier) || !seen.Add(identifier))
            {
                throw new Http3ConnectionException(Http3ErrorCode.SettingsError, $"the client's SETTINGS repeats setting 0x{identifier:x} or carries it from HTTP/2");
            }

            payload = payload[length..];
        }
    }

    /// <summary>HTTP/2's settings, which HTTP/3 reserves (RFC 9114 section 7.2.4.1).</summary>
    private static bool IsHttp2Setting(ulong identifier) => identifier is >= 0x02 and <= 0x05;

    // One identifier and value pair: the identifier and how many bytes the pair took.
    private static (ulong Identifier, int Length) ReadSetting(ReadOnlySpan<byte> payload)
    {
        if (!QuicVariableLengthInteger.TryRead(payload, out var identifier, out var identifierLength)
            || !QuicVariableLengthInteger.TryRead(payload[identifierLength..], out _, out var valueLength))
        {
            throw new Http3ConnectionException(Http3ErrorCode.FrameError, "the client's SETTINGS ends part way through a setting");
        }

        return (identifier, identifierLength + valueLength);
    }
}
