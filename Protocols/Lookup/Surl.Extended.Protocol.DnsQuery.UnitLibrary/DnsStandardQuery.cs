namespace Surl.Extended.Protocol.DnsQuery;

/// <summary>
/// A standard query with one question, read whole (<see cref="DnsQueryAnswerer.StandardQueryOf"/>).
/// </summary>
/// <param name="Header">Its header.</param>
/// <param name="Question">Its one question.</param>
/// <param name="Opt">Its one OPT record, or <see langword="null"/> when it had none.</param>
internal sealed record DnsStandardQuery(DnsHeader Header, DnsQuestion Question, DnsOptRecord? Opt);
