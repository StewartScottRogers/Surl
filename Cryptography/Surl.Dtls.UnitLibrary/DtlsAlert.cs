namespace Surl.Dtls;

/// <summary>
/// One alert record's plaintext (RFC 5246 section 7.2): its level, warning (1) or fatal (2), and
/// its description.
/// </summary>
/// <param name="IsFatal">Whether the level is fatal (2) rather than warning (1).</param>
/// <param name="Description">What the alert says.</param>
public readonly record struct DtlsAlert(bool IsFatal, DtlsAlertDescription Description)
{
    /// <summary>The length of an alert's plaintext: 2 bytes.</summary>
    public const int Size = 2;

    private const byte WarningLevel = 1;
    private const byte FatalLevel = 2;

    /// <summary>Reads an alert record's plaintext.</summary>
    /// <param name="plaintext">The record's plaintext.</param>
    /// <param name="alert">The alert, when the plaintext is two bytes.</param>
    /// <returns><see langword="false"/> when the plaintext is not two bytes.</returns>
    public static bool TryRead(ReadOnlySpan<byte> plaintext, out DtlsAlert alert)
    {
        alert = default;
        if (plaintext.Length != Size)
        {
            return false;
        }

        alert = new DtlsAlert(plaintext[0] == FatalLevel, (DtlsAlertDescription)plaintext[1]);
        return true;
    }

    /// <summary>Writes the alert's plaintext.</summary>
    /// <returns>The level, then the description.</returns>
    public byte[] Write() => [IsFatal ? FatalLevel : WarningLevel, (byte)Description];
}
