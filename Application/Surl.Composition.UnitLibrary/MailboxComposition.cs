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
    public MailDomainConfiguration? MailDomains { get; }

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

        var mailDomains = commandLine.MailDomains.Count == 0
            ? null
            : new MailDomainConfiguration(commandLine.MailDomains, mailboxes.Select(mailbox => mailbox.Address), [], []);
        return (new MailboxComposition(mailboxes, mailDomains), SurlExitCode.Ok, null);
    }

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

        log.WriteLine($"* Mail domains: {string.Join(", ", commandLine.MailDomains.Select(MailDomainNormalised))}");
        if (commandLine.AllowAnonymous)
        {
            log.WriteLine("* Mail domains not applied: --allow-anonymous delivers every recipient to one mailbox set");
        }

        foreach (var (account, mailbox) in Accounts.Zip(mailboxes))
        {
            log.WriteLine($"* Mailbox {account.UserName}: logins on {string.Join(", ", mailbox.Protocols ?? DefaultSchemeNames)}");
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
