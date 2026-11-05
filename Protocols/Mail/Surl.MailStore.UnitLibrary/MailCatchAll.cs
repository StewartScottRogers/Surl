namespace Surl.MailStore;

/// <summary>
/// One <c>--mail-catch-all</c> (ADR-0102, decision 1.3): mail to a local part of
/// <paramref name="Domain"/> that names no alias, mailbox or account is delivered to whatever
/// <paramref name="Target"/> resolves to.
/// </summary>
/// <param name="Domain">A configured mail domain.</param>
/// <param name="Target">The address it delivers to, in a configured mail domain.</param>
public sealed record MailCatchAll(string Domain, string Target);
