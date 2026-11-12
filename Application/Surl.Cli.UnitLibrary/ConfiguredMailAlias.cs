namespace Surl.Cli;

/// <summary>
/// One <c>--mail-alias &lt;address&gt;=&lt;address&gt;,...</c> (ADR-0101 decision 4, ADR-0102 decision 1.3): mail to
/// <see cref="Address"/> is delivered to each of <see cref="Targets"/>. Read by <see cref="ConfiguredMailAliasReader"/>;
/// the record holds what was written, percent-decoded. Its domain is matched by <see cref="ConfiguredMailConflicts"/>,
/// and whether each target reaches a mailbox is the mail store's to check at start (BL-994).
/// </summary>
/// <param name="Address">The alias's address as written, one <see cref="MailDomainNames.ReadAddress"/> accepts.</param>
/// <param name="Targets">The target addresses as written, in order, each one <see cref="MailDomainNames.ReadAddress"/> accepts; no target twice.</param>
public sealed record ConfiguredMailAlias(string Address, IReadOnlyList<string> Targets);
