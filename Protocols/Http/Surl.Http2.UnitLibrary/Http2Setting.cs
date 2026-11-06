namespace Surl.Http2;

/// <summary>
/// One 6-byte entry of a <c>SETTINGS</c> frame (RFC 9113 section 6.5.1): a 16-bit identifier and
/// a 32-bit value.
/// </summary>
/// <param name="Identifier">The setting, possibly one RFC 9113 does not define.</param>
/// <param name="Value">Its value.</param>
public readonly record struct Http2Setting(Http2SettingIdentifier Identifier, uint Value)
{
    /// <summary>The bytes one entry takes on the wire.</summary>
    public const int EncodedLength = 6;
}
