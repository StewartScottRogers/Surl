using Surl.Cli;

namespace Surl.Composition;

/// <summary>
/// Refuses a <c>protocols=</c> list that names a scheme whose registered server makes no login (ADR-0102
/// decision 4.3, amendment of 2026-11-20): such an account would start and be reachable nowhere.
/// </summary>
public static class SchemeLoginCheck
{
    /// <summary>
    /// The one definition of "no login": every scheme of a registration that marks itself as making no login.
    /// </summary>
    /// <param name="registrations">The registrations to ask.</param>
    /// <returns>The schemes, compared ordinally.</returns>
    public static HashSet<string> SchemesWithoutLogin(IEnumerable<ProtocolServerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        return registrations
            .Where(registration => !registration.MakesLogin)
            .SelectMany(registration => registration.Schemes)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Finds the first scheme in the <c>protocols=</c> lists of the mailboxes and of <c>--user-ldap</c> that a
    /// registration in <paramref name="registrations"/> lists and marks as making no login.
    /// A scheme no registration lists is left to the parse-time <c>unknown scheme</c> refusal.
    /// </summary>
    /// <param name="mailboxProtocols">The <c>protocols=</c> list of each mailbox; <see langword="null"/> when a mailbox gave none.</param>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="registrations">The registrations to ask.</param>
    /// <returns>The refusal line, <c>(2) scheme &lt;scheme&gt; has no login</c>, or <see langword="null"/>.</returns>
    public static string? FindRefusal(
        IEnumerable<IReadOnlyList<string>?> mailboxProtocols,
        SurlCommandLine commandLine,
        IEnumerable<ProtocolServerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(mailboxProtocols);
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(registrations);
        var withoutLogin = SchemesWithoutLogin(registrations);
        var named = mailboxProtocols.Append(commandLine.UserLdap?.Protocols).SelectMany(list => list ?? []);
        var found = named.FirstOrDefault(scheme => withoutLogin.Contains(scheme.ToLowerInvariant()));
        return found is null ? null : $"(2) scheme {found.ToLowerInvariant()} has no login";
    }
}
