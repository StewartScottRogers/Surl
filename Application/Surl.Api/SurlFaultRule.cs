namespace Surl.Api;

/// <summary>
/// One rule in a running <see cref="SurlServer"/>'s fault plan, as <see cref="SurlServerFaults.List"/> reads it
/// (ADR-0124, decisions 4 and 8: <c>list_faults</c>'s <c>fault_id</c>, <c>item</c> and <c>remaining</c>).
/// </summary>
/// <param name="Id">The rule's identifier, <c>f&lt;n&gt;</c>, which <see cref="SurlServerFaults.Remove"/> takes.</param>
/// <param name="Item">
/// The <c>--fault</c> item the rule spells, every value percent-encoded so <c>surl</c>'s reader reads it back as the
/// same rule; it leaves out <c>times</c>, which <paramref name="Remaining"/> carries, so a spent rule still spells an
/// item <c>surl</c> takes. E.g. <c>drop;listener=smtp%3A%2F%2F127.0.0.1%3A0</c>.
/// </param>
/// <param name="Remaining">
/// How many more times the rule applies before it is spent, 0 once it is; <see langword="null"/> for a rule without
/// <c>times</c>, which always applies.
/// </param>
public sealed record SurlFaultRule(string Id, string Item, int? Remaining);
