using Surl.Quic;

namespace Surl.Http3;

/// <summary>
/// Reads the client's <c>SETTINGS</c> payload (RFC 9114 section 7.2.4). The connection itself needs
/// none of its values - its encoder never uses a dynamic table and its answers are small - so it
/// enforces the rules and hands the values to extended <c>CONNECT</c> sessions, whose protocols may
/// require some (RFC 9297 section 2.1.1).
/// </summary>
internal static class Http3Settings
{
    /// <summary>
    /// Reads the payload's settings, or throws when it is not whole identifier and value pairs
    /// (<c>H3_FRAME_ERROR</c>), or repeats an identifier or carries one of HTTP/2's, 0x02 to 0x05
    /// (<c>H3_SETTINGS_ERROR</c>).
    /// </summary>
    /// <param name="payload">The <c>SETTINGS</c> frame's payload.</param>
    /// <returns>Identifier to value.</returns>
    /// <exception cref="Http3ConnectionException">The payload breaks a rule.</exception>
    public static Dictionary<ulong, ulong> Read(ReadOnlySpan<byte> payload)
    {
        var settings = new Dictionary<ulong, ulong>();
        while (!payload.IsEmpty)
        {
            var (identifier, value, length) = ReadSetting(payload);
            if (IsHttp2Setting(identifier) || !settings.TryAdd(identifier, value))
            {
                throw new Http3ConnectionException(Http3ErrorCode.SettingsError, $"the client's SETTINGS repeats setting 0x{identifier:x} or carries it from HTTP/2");
            }

            payload = payload[length..];
        }

        return settings;
    }

    /// <summary>HTTP/2's settings, which HTTP/3 reserves (RFC 9114 section 7.2.4.1).</summary>
    private static bool IsHttp2Setting(ulong identifier) => identifier is >= 0x02 and <= 0x05;

    // One identifier and value pair, and how many bytes it took.
    private static (ulong Identifier, ulong Value, int Length) ReadSetting(ReadOnlySpan<byte> payload)
    {
        if (!QuicVariableLengthInteger.TryRead(payload, out var identifier, out var identifierLength)
            || !QuicVariableLengthInteger.TryRead(payload[identifierLength..], out var value, out var valueLength))
        {
            throw new Http3ConnectionException(Http3ErrorCode.FrameError, "the client's SETTINGS ends part way through a setting");
        }

        return (identifier, value, identifierLength + valueLength);
    }
}
