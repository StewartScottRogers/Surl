namespace Surl.Http2;

/// <summary>
/// Decodes a <c>SETTINGS</c> frame's payload on its own, as the <c>HTTP2-Settings</c> field of an
/// <c>Upgrade: h2c</c> request carries it (RFC 7540 section 3.2.1; ADR-0086, decision 1).
/// </summary>
public static class Http2SettingsPayload
{
    /// <summary>
    /// Decodes <paramref name="payload"/> as the frame parser decodes a <c>SETTINGS</c> frame's.
    /// </summary>
    /// <param name="payload">The payload: whole 6-byte entries.</param>
    /// <returns>The settings, or <see langword="null"/> when the payload is not whole entries the parser accepts.</returns>
    public static IReadOnlyList<Http2Setting>? Decode(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var result = Http2FrameParser.Parse(new Http2FrameHeader(payload.Length, (byte)Http2FrameType.Settings, 0, 0), payload);

        return result.Frame is Http2SettingsFrame settings ? settings.Settings : null;
    }
}
