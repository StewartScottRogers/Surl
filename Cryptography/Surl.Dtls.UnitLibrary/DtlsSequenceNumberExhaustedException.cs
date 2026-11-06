namespace Surl.Dtls;

/// <summary>
/// Thrown when a record is sealed after the epoch's last 48-bit sequence number was used: RFC 6347
/// section 4.1 forbids wrapping, so the association has to close (ADR-0087 decision 8).
/// </summary>
public sealed class DtlsSequenceNumberExhaustedException : InvalidOperationException
{
    /// <summary>Creates the exception for <paramref name="epoch"/>.</summary>
    /// <param name="epoch">The epoch whose sequence numbers are used up.</param>
    public DtlsSequenceNumberExhaustedException(ushort epoch)
        : base($"Epoch {epoch} has used every 48-bit sequence number; the association must close.")
    {
        Epoch = epoch;
    }

    /// <summary>The epoch whose sequence numbers are used up.</summary>
    public ushort Epoch { get; }
}
