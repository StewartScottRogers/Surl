namespace Surl.Dtls;

/// <summary>
/// Ends a handshake with the fatal alert <see cref="Description"/> (ADR-0087 decision 10).
/// </summary>
internal sealed class DtlsAlertException(DtlsAlertDescription description, string message) : Exception(message)
{
    /// <summary>The alert the server sends.</summary>
    public DtlsAlertDescription Description { get; } = description;
}
