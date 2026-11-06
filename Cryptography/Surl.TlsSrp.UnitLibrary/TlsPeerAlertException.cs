namespace Surl.TlsSrp;

/// <summary>
/// The client sent an alert that ends the connection: any alert during the handshake, or a fatal
/// one after it (RFC 5246 section 7.2). The server sends nothing in reply (ADR-0122 decision 6).
/// </summary>
/// <param name="isFatal">Whether the alert's level was fatal (2) rather than warning (1).</param>
/// <param name="description">The alert's description byte.</param>
public sealed class TlsPeerAlertException(bool isFatal, byte description)
    : IOException($"The client sent the {(isFatal ? "fatal" : "warning")} alert {description}.")
{
    /// <summary>Whether the alert's level was fatal.</summary>
    public bool IsFatal { get; } = isFatal;

    /// <summary>The alert's description byte, e.g. 20 for <c>bad_record_mac</c>.</summary>
    public byte Description { get; } = description;
}
