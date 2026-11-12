using Surl.Authentication;
using Surl.Cli;
using Surl.MailStore;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// The mail domains and mailboxes a start serves (ADR-0102, decisions 1.1, 1.2, 1.4 and 5): every
/// <c>--mailbox</c> and then every <c>--mailbox-file</c> record, each an account named by its
/// normalised address and allowed on its <c>protocols=</c> schemes (the mail schemes when absent),
/// and the <see cref="MailDomainConfiguration"/> the mail store resolves recipients against. With no
/// <c>--mail-domain</c> there is no configuration, and the store resolves as ADR-0050 decision 5 says.
/// </summary>
public sealed class MailboxComposition
{
    // The schemes a mailbox with no protocols= logs in on, as its note names them (Account.MailSchemes).
    private static readonly string[] DefaultSchemeNames = ["smtp", "imap", "pop3"];

    private const string SharedFolderOption = "shared-folder";
    private const string MailAliasOption = "mail-alias";
    private const string MailCatchAllOption = "mail-catch-all";

    private readonly IReadOnlyList<ConfiguredMailbox> mailboxes;

    private MailboxComposition(IReadOnlyList<ConfiguredMailbox> mailboxes, MailDomainConfiguration? mailDomains)
    {
        this.mailboxes = mailboxes;
        MailDomains = mailDomains;
        Accounts = mailDomains is null
            ? []
            : [.. mailDomains.MailboxAddresses.Zip(mailboxes, (address, mailbox) => new Account(address, mailbox.Password) { Schemes = SchemesOf(mailbox) })];
    }

    /// <summary>
    /// The configuration the mail store resolves recipients against; <see langword="null"/> without <c>--mail-domain</c>.
    /// </summary>
    public MailDomainConfiguration? MailDomains { get; private set; }

    /// <summary>
    /// One account per mailbox, in order, named by its normalised address and restricted to its schemes.
    /// </summary>
    public IReadOnlyList<Account> Accounts { get; }

    /// <summary>
    /// Reads every <c>--mailbox-file</c> through <paramref name="readFile"/>, after the <c>--mailbox</c>es,
    /// and composes the mail domains (ADR-0101 decision 5 rows 16 and 17).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="readFile">Reads a file's bytes, given its path as given.</param>
    /// <param name="extendedSchemes">The schemes the registered extended servers claim, which <c>protocols=</c> may name.</param>
    /// <returns>
    /// The composition; or, when a file cannot be read (37) or holds a malformed record (2),
    /// <see langword="null"/> with the exit code and the message after <c>surl: </c>, never holding a password.
    /// </returns>
    public static (MailboxComposition? Composition, SurlExitCode ExitCode, string? FailureMessage) Compose(
        SurlCommandLine commandLine, Func<string, byte[]> readFile, IReadOnlyCollection<string> extendedSchemes)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(readFile);
        var reader = new MailboxFileReader(commandLine, extendedSchemes);
        List<ConfiguredMailbox> mailboxes = [.. commandLine.Mailboxes];
        foreach (var file in commandLine.MailboxFiles)
        {
            byte[] content;
            try
            {
                content = readFile(file);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                return (null, SurlExitCode.CouldNotReadFile, $"(37) Could not read mailbox file {file}");
            }

            if (reader.Read(file, content, out var read) is { } refusal)
            {
                return (null, SurlExitCode.FailedInit, refusal);
            }

            mailboxes.AddRange(read);
        }

        MailDomainConfiguration? mailDomains = null;
        if (commandLine.MailDomains.Count != 0)
        {
            try
            {
                mailDomains = BuildConfiguration(commandLine, mailboxes, []);
            }
            catch (ArgumentException refusal)
            {
                return (null, SurlExitCode.FailedInit, $"(2) mail configuration: {WithoutParameterName(refusal.Message)}");
            }
        }

        return (new MailboxComposition(mailboxes, mailDomains), SurlExitCode.Ok, null);
    }

    /// <summary>
    /// Adds every <c>--shared-folder</c> to <see cref="MailDomains"/> once the groups are expanded
    /// (ADR-0102 decision 1.6): a <c>@group</c> member becomes the account names it holds, and
    /// <c>@everyone</c> is refused. Shared folders need no mail domain, so a start with only shared
    /// folders gets a configuration holding them.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="groups">The groups, expanded over every account.</param>
    /// <returns>Null, or the refusal after <c>surl: </c>, which exits <see cref="SurlExitCode.FailedInit"/> (2).</returns>
    public string? ComposeSharedFolders(SurlCommandLine commandLine, AccountGroupExpansion groups)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(groups);
        List<MailSharedFolder> folders = [];
        foreach (var folder in commandLine.SharedFolders)
        {
            if (folder.Members.Any(member => member.IsGroup && member.Name == AccountGroupExpansion.Everyone))
            {
                return AccountGroupExpansion.EveryoneIsNotAllowed(SharedFolderOption);
            }

            if (groups.Expand(SharedFolderOption, folder.Members, out var accounts) is { } failure)
            {
                return failure;
            }

            folders.Add(new MailSharedFolder(folder.Name, [.. accounts]));
        }

        if (folders.Count == 0)
        {
            return null;
        }

        try
        {
            MailDomains = BuildConfiguration(commandLine, mailboxes, folders);
        }
        catch (ArgumentException refusal)
        {
            return $"(2) option --{SharedFolderOption}: {WithoutParameterName(refusal.Message)}";
        }

        return null;
    }

    /// <summary>
    /// Writes ADR-0102 decision 1.3's catch-all warning, <c>surl: warning: --mail-catch-all: &lt;domain&gt; keeps
    /// mail to every unknown address</c>, for each catch-all, from the info level up (ADR-0101 decision 4).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="log">The log stream.</param>
    public static void WriteWarnings(SurlCommandLine commandLine, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(log);
        if (commandLine.LogLevel < LogLevel.Info)
        {
            return;
        }

        foreach (var catchAll in commandLine.MailCatchAlls)
        {
            log.WriteLine($"surl: warning: --{MailCatchAllOption}: {MailDomainNormalised(catchAll.Domain)} keeps mail to every unknown address, which would otherwise be discarded");
        }
    }

    /// <summary>
    /// The refusal for a route <see cref="MailRouteException"/> reports at start (ADR-0102 decision 1.3).
    /// </summary>
    /// <param name="failure">The store's refusal.</param>
    /// <returns><c>(2) option --mail-alias: target &lt;address&gt; is not in any --mail-domain</c>,
    /// <c>... names no mailbox</c> or <c>alias &lt;address&gt; includes itself</c>, naming <c>--mail-catch-all</c> for a catch-all.</returns>
    public static string DescribeRouteFailure(MailRouteException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var (option, word) = failure.RouteKind == MailRouteKind.CatchAll ? (MailCatchAllOption, "catch-all") : (MailAliasOption, "alias");
        var problem = failure.Problem switch
        {
            MailRouteProblem.TargetOutsideMailDomains => $"target {failure.Address} is not in any --mail-domain",
            MailRouteProblem.TargetNamesNoMailbox => $"target {failure.Address} names no mailbox",
            _ => $"{word} {failure.Address} includes itself",
        };
        return $"(2) option --{option}: {problem}";
    }

    private static MailDomainConfiguration BuildConfiguration(
        SurlCommandLine commandLine, IReadOnlyList<ConfiguredMailbox> mailboxes, IReadOnlyList<MailSharedFolder> sharedFolders) =>
        new(
            commandLine.MailDomains,
            mailboxes.Select(mailbox => mailbox.Address),
            commandLine.MailAliases.Select(alias => new MailAlias(alias.Address, alias.Targets)),
            commandLine.MailCatchAlls.Select(catchAll => new MailCatchAll(catchAll.Domain, catchAll.Address)),
            mailboxes.Where(mailbox => mailbox.QuotaBytes is not null || mailbox.Folders.Count != 0)
                .Select(mailbox => new MailboxSettings(mailbox.Address, mailbox.QuotaBytes, mailbox.Folders)),
            sharedFolders);

    private static string QuotaAndFolders(ConfiguredMailbox mailbox) =>
        (mailbox.QuotaBytes is { } bytes ? $", quota {bytes} bytes" : string.Empty)
        + (mailbox.Folders.Count == 0 ? string.Empty : $", {mailbox.Folders.Count} folders");

    private static string WithoutParameterName(string message) => message.Split(" (Parameter ")[0];

    /// <summary>
    /// Writes ADR-0102 decision 5's notes from the verbose level up: <c>* Mail domains: &lt;domains&gt;</c>,
    /// under <c>--allow-anonymous</c> that they are not applied, then
    /// <c>* Mailbox &lt;address&gt;: logins on &lt;schemes&gt;</c> for each mailbox.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="log">The log stream.</param>
    public void WriteStartLines(SurlCommandLine commandLine, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(log);
        if (commandLine.LogLevel < LogLevel.Verbose || MailDomains is null)
        {
            return;
        }

        if (commandLine.MailDomains.Count != 0)
        {
            log.WriteLine($"* Mail domains: {string.Join(", ", commandLine.MailDomains.Select(MailDomainNormalised))}");
        }

        if (commandLine.MailDomains.Count != 0 && commandLine.AllowAnonymous)
        {
            log.WriteLine("* Mail domains not applied: --allow-anonymous delivers every recipient to one mailbox set");
        }

        foreach (var (account, mailbox) in Accounts.Zip(mailboxes))
        {
            log.WriteLine($"* Mailbox {account.UserName}: logins on {string.Join(", ", mailbox.Protocols ?? DefaultSchemeNames)}{QuotaAndFolders(mailbox)}");
        }

        foreach (var folder in MailDomains.SharedFolders)
        {
            log.WriteLine($"* Shared folder Shared/{folder.Name}: {folder.Members.Count} members");
        }
    }

    private static string MailDomainNormalised(string domain)
    {
        MailDomainName.TryNormalise(domain, out var asciiDomain);
        return asciiDomain;
    }

    private static IReadOnlySet<string> SchemesOf(ConfiguredMailbox mailbox) =>
        mailbox.Protocols is { } protocols ? new HashSet<string>(protocols, StringComparer.OrdinalIgnoreCase) : Account.MailSchemes;
}
