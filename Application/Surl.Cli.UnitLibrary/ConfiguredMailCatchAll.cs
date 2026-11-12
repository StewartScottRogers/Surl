namespace Surl.Cli;

/// <summary>
/// One <c>--mail-catch-all &lt;domain&gt;=&lt;address&gt;</c> (ADR-0101 decision 4, ADR-0102 decision 1.3): mail to an
/// address of <see cref="Domain"/> that no alias, mailbox or account takes is delivered to <see cref="Address"/>.
/// It widens access, so it is warned at every start (BL-994). Read by <see cref="ConfiguredMailCatchAllReader"/>;
/// the record holds what was written, percent-decoded.
/// </summary>
/// <param name="Domain">The domain as written, one <see cref="MailDomainNames.ReadDomain"/> accepts.</param>
/// <param name="Address">The target address as written, one <see cref="MailDomainNames.ReadAddress"/> accepts.</param>
public sealed record ConfiguredMailCatchAll(string Domain, string Address);
