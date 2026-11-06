namespace Surl.TlsSrp;

/// <summary>
/// Ends the connection with the fatal alert <see cref="Description"/> (ADR-0122 decision 6).
/// </summary>
/// <param name="description">The alert the server sends.</param>
/// <param name="message">What went wrong, for the verbose log; never sent on the wire.</param>
public sealed class TlsAlertException(TlsAlertDescription description, string message) : Exception(message)
{
    /// <summary>The alert the server sends.</summary>
    public TlsAlertDescription Description { get; } = description;
}
