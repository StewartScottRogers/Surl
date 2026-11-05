namespace Surl.Quic;

/// <summary>A contiguous run of acknowledged packet numbers, both ends included (RFC 9000 section 19.3.1).</summary>
/// <param name="Smallest">The smallest packet number acknowledged in the run.</param>
/// <param name="Largest">The largest packet number acknowledged in the run.</param>
public readonly record struct QuicAckRange(ulong Smallest, ulong Largest);
