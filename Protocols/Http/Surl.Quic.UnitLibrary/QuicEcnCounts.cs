namespace Surl.Quic;

/// <summary>The ECN counts an <c>ACK</c> frame of type <c>0x03</c> carries (RFC 9000 section 19.3.2).</summary>
/// <param name="Ect0">Packets received with the ECT(0) codepoint.</param>
/// <param name="Ect1">Packets received with the ECT(1) codepoint.</param>
/// <param name="EcnCe">Packets received with the ECN-CE codepoint.</param>
public sealed record QuicEcnCounts(ulong Ect0, ulong Ect1, ulong EcnCe);
