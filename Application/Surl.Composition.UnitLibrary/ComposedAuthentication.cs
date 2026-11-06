using Surl.Authentication;
using Surl.Kerberos;

namespace Surl.Composition;

/// <summary>
/// What the servers judge logins by, the keytab entries skipped, and what the SSH server proves
/// itself with: the start's authentication, composed before the data-directory lock is taken.
/// </summary>
/// <param name="Policy">The authentication policy.</param>
/// <param name="AccountNames">Every configured account's user name, the mail store's owners.</param>
/// <param name="AccountGroups">Every <c>--group</c>, expanded to account names (ADR-0102 decision 4.1).</param>
/// <param name="SkippedKeytabEntries">The <c>--keytab</c> entries of an enctype surl does not accept.</param>
/// <param name="SshHostKeys">The SSH host keys.</param>
/// <param name="SchemePolicies">The policy each server is composed with, by its scheme (ADR-0102 decision 4.3).</param>
internal sealed record ComposedAuthentication(
    AuthenticationPolicy Policy,
    IReadOnlyList<string> AccountNames,
    AccountGroupExpansion AccountGroups,
    IReadOnlyList<KerberosKeytabSkippedEntry> SkippedKeytabEntries,
    SshHostKeyComposition SshHostKeys,
    SchemeAuthenticationPolicies SchemePolicies);
