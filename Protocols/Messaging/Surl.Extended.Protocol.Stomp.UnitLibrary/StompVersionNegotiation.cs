namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// Chooses the version a connection speaks from CONNECT's <c>accept-version</c> header (ADR-0118
/// decision 1; the specification's "Protocol Negotiation").
/// </summary>
internal static class StompVersionNegotiation
{
    /// <summary>What CONNECTED's <c>version</c> header and the version ERROR's say the server speaks.</summary>
    public const string SupportedVersions = "1.0,1.1,1.2";

    /// <summary>
    /// The highest version in <paramref name="acceptVersion"/> the server speaks: 1.0 when the
    /// header is absent, as the specification says, and <see langword="null"/> when it names
    /// none of the three.
    /// </summary>
    /// <param name="acceptVersion">CONNECT's <c>accept-version</c> header, or <see langword="null"/>.</param>
    public static StompVersion? ChooseHighest(string? acceptVersion)
    {
        if (acceptVersion is null)
        {
            return StompVersion.V1_0;
        }

        StompVersion? highest = null;
        foreach (var offered in acceptVersion.Split(','))
        {
            if (TryParse(offered, out var version) && (highest is null || version > highest))
            {
                highest = version;
            }
        }

        return highest;
    }

    /// <summary>The version as CONNECTED's <c>version</c> header writes it: <c>1.0</c>, <c>1.1</c> or <c>1.2</c>.</summary>
    /// <param name="version">The version.</param>
    public static string Text(StompVersion version) => version switch
    {
        StompVersion.V1_0 => "1.0",
        StompVersion.V1_1 => "1.1",
        _ => "1.2",
    };

    private static bool TryParse(string text, out StompVersion version)
    {
        (var known, version) = text switch
        {
            "1.0" => (true, StompVersion.V1_0),
            "1.1" => (true, StompVersion.V1_1),
            "1.2" => (true, StompVersion.V1_2),
            _ => (false, StompVersion.V1_0),
        };
        return known;
    }
}
