namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// What <see cref="DnsMessageReader.Read"/> made of a message: the whole message, or the fault
/// that stopped it with whatever was read before it, so a <c>FORMERR</c> answer can echo the
/// header and the question (ADR-0092 decision 2).
/// </summary>
/// <param name="Fault">Why reading stopped, or <see cref="DnsReadFault.None"/>.</param>
/// <param name="Header">The header, or <see langword="null"/> when there was none to read.</param>
/// <param name="Questions">The questions read before the fault, every one when there was none.</param>
/// <param name="Message">The whole message, or <see langword="null"/> after a fault.</param>
public sealed record DnsReadOutcome(
    DnsReadFault Fault,
    DnsHeader? Header,
    IReadOnlyList<DnsQuestion> Questions,
    DnsMessage? Message);
