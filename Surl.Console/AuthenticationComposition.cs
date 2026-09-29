using Surl.Authentication;
using Surl.Cli;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// The authentication policy the HTTP and MQTT servers are given, as the command line asks for
/// it (ADR-0032): the accounts from every <c>--user</c> and then the <c>--user-file</c>, the
/// methods <c>--auth</c> accepts, and <c>--allow-anonymous</c> and
/// <c>--allow-plaintext-auth</c>; and the warning line each loosening option writes on start.
/// </summary>
internal static class AuthenticationComposition
{
    private const string WarningPrefix = "surl: warning: ";

    // The --auth words this build implements a method for; any other --auth word is refused
    // before any listener binds (ADR-0032, section 1), until its method lands.
    private static readonly Dictionary<string, AuthenticationMethod> ImplementedMethodsByWord = new(StringComparer.Ordinal)
    {
        ["digest"] = AuthenticationMethod.Digest,
        ["basic"] = AuthenticationMethod.Basic,
        ["bearer"] = AuthenticationMethod.Bearer,
    };

    /// <summary>
    /// Builds the policy: refuses an <c>--auth</c> word whose method this build does not
    /// implement, reads the <c>--user-file</c> through <paramref name="readUserFile"/> when one
    /// was given, and composes the Basic, Bearer and Digest methods over the accounts.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="readUserFile">Reads the <c>--user-file</c>'s bytes, given its path as given.</param>
    /// <param name="timeProvider">The clock the refusal delay and the Digest nonces run on.</param>
    /// <returns>
    /// The policy; or, when it cannot be built, <see langword="null"/> with the exit code and the
    /// message after the <c>surl: </c> prefix (ADR-0032, sections 1 and 2).
    /// </returns>
    public static (AuthenticationPolicy? Policy, SurlExitCode ExitCode, string? FailureMessage) Compose(
        SurlCommandLine commandLine, Func<string, byte[]> readUserFile, TimeProvider timeProvider)
    {
        if (FindUnavailableMethodWord(commandLine) is { } word)
        {
            return (null, SurlExitCode.FailedInit, $"(2) --auth {word} is not available in this build");
        }

        var (accounts, exitCode, failureMessage) = ReadAccounts(commandLine, readUserFile);
        return accounts is null
            ? (null, exitCode, failureMessage)
            : (ComposePolicy(ComposeSettings(commandLine, accounts), timeProvider), SurlExitCode.Ok, null);
    }

    /// <summary>
    /// Builds the policy of a command line with no accounts and no loosening option: what the
    /// servers are composed with when only their schemes are wanted.
    /// </summary>
    /// <param name="timeProvider">The clock the refusal delay and the Digest nonces run on.</param>
    /// <returns>The policy.</returns>
    public static AuthenticationPolicy ComposeWithoutAccounts(TimeProvider timeProvider) =>
        ComposePolicy(ComposeSettings(new SurlCommandLine(), []), timeProvider);

    /// <summary>
    /// Builds the settings the policy judges by: an account book of <paramref name="accounts"/>,
    /// the two loosening switches, and the methods <c>--auth</c> accepts (its default set without it).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="accounts">Every account, the <c>--user</c> ones first.</param>
    /// <returns>The settings.</returns>
    public static AuthenticationSettings ComposeSettings(SurlCommandLine commandLine, IReadOnlyList<Account> accounts) =>
        new(
            new AccountBook(accounts),
            commandLine.AllowAnonymous,
            commandLine.AllowPlaintextAuthentication,
            commandLine.GivenAuthenticationMethods is { } words
                ? words.Select(word => ImplementedMethodsByWord[word]).ToHashSet()
                : AuthenticationMethods.DefaultAccepted);

    /// <summary>
    /// Writes ADR-0032 section 9's warning line for each loosening option given, in its order,
    /// from the info level up (ADR-0033, section 7); the <c>--self-signed</c> line is not one of
    /// them, since it is written only when the throwaway certificate is made.
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="log">The log stream.</param>
    public static void WriteLooseningWarnings(SurlCommandLine commandLine, TextWriter log)
    {
        if (commandLine.LogLevel < LogLevel.Info)
        {
            return;
        }

        if (commandLine.AllowAnonymous)
        {
            log.WriteLine(WarningPrefix + "--allow-anonymous: every request and login is accepted without checking credentials");
        }

        if (commandLine.AllowPlaintextAuthentication)
        {
            log.WriteLine(WarningPrefix + "--allow-plaintext-auth: passwords and tokens are accepted over unencrypted connections");
        }

        if (commandLine.GivenAuthenticationMethods is { } words)
        {
            log.WriteLine(WarningPrefix + $"--auth: accepted methods are {string.Join(", ", words)}");
        }
    }

    private static string? FindUnavailableMethodWord(SurlCommandLine commandLine) =>
        commandLine.GivenAuthenticationMethods?.FirstOrDefault(word => !ImplementedMethodsByWord.ContainsKey(word));

    // The --user accounts, then the --user-file's (ADR-0032, section 2): a file that cannot be
    // read is 37, a malformed one 2, each naming the file as given and never a password.
    private static (IReadOnlyList<Account>? Accounts, SurlExitCode ExitCode, string? FailureMessage) ReadAccounts(
        SurlCommandLine commandLine, Func<string, byte[]> readUserFile)
    {
        IReadOnlyList<Account> userOptionAccounts =
            [.. commandLine.Accounts.Select(account => new Account(account.UserName, account.Password))];
        if (commandLine.UserFile is not { } userFile)
        {
            return (userOptionAccounts, SurlExitCode.Ok, null);
        }

        byte[] content;
        try
        {
            content = readUserFile(userFile);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            return (null, SurlExitCode.CouldNotReadFile, $"(37) Could not read user file {userFile}");
        }

        var parsed = UserFileParser.Parse(content, userOptionAccounts);
        return parsed.Failure is { } lineFailure
            ? (null, SurlExitCode.FailedInit, $"(2) User file {userFile}, {lineFailure.Describe()}")
            : (parsed.Accounts, SurlExitCode.Ok, null);
    }

    private static AuthenticationPolicy ComposePolicy(AuthenticationSettings settings, TimeProvider timeProvider) =>
        new(
            settings,
            [
                new BasicAuthenticationMethod(settings.Accounts),
                new BearerAuthenticationMethod(settings.Accounts),
                new DigestAuthenticationMethod(settings.Accounts, timeProvider),
            ],
            timeProvider);
}
