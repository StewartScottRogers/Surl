namespace Surl.MailStore;

/// <summary>
/// One <c>--mail-alias</c> (ADR-0102, decision 1.3): mail to <paramref name="Address"/> is
/// delivered to whatever each of <paramref name="Targets"/> resolves to.
/// </summary>
/// <param name="Address">The alias's address, <c>local@domain</c>, in a configured mail domain.</param>
/// <param name="Targets">The addresses it delivers to, each in a configured mail domain.</param>
public sealed record MailAlias(string Address, IReadOnlyList<string> Targets);
