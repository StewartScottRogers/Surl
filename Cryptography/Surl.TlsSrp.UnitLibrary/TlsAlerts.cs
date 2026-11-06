namespace Surl.TlsSrp;

/// <summary>
/// The two-byte body of an alert record (RFC 5246 section 7.2): its level, 1 for warning and 2 for
/// fatal, then its description.
/// </summary>
internal static class TlsAlerts
{
    private const byte WarningLevel = 1;
    private const byte FatalLevel = 2;

    /// <summary>A fatal alert's body.</summary>
    /// <param name="description">The alert.</param>
    public static byte[] Fatal(TlsAlertDescription description) => [FatalLevel, (byte)description];

    /// <summary>A warning alert's body.</summary>
    /// <param name="description">The alert.</param>
    public static byte[] Warning(TlsAlertDescription description) => [WarningLevel, (byte)description];

    /// <summary>Reads an alert record the client sent.</summary>
    /// <param name="fragment">The alert record's fragment.</param>
    /// <returns>The alert, as the exception that ends the connection.</returns>
    /// <exception cref="TlsAlertException"><see cref="TlsAlertDescription.DecodeError"/> when the fragment is not two bytes.</exception>
    public static TlsPeerAlertException ReadPeerAlert(byte[] fragment)
    {
        if (fragment.Length != 2)
        {
            throw new TlsAlertException(TlsAlertDescription.DecodeError, $"The client's alert record is {fragment.Length} bytes, not 2.");
        }

        return new TlsPeerAlertException(fragment[0] == FatalLevel, fragment[1]);
    }
}
