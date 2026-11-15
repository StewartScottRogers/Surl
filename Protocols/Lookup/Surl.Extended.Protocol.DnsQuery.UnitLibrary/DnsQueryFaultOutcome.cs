using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// What the exchange's fault plan decided for one query (<see cref="DnsQueryFaults.FindAsync"/>):
/// answer it as usual, answer it with a chosen RCODE, or leave it unanswered.
/// </summary>
/// <param name="ChosenError">The chosen error answer, or <see langword="null"/> when there is none.</param>
/// <param name="IsDropped">Whether a <see cref="FaultKind.Drop"/> rule applies: the query gets no answer.</param>
internal readonly record struct DnsQueryFaultOutcome(DnsResponse? ChosenError, bool IsDropped);
