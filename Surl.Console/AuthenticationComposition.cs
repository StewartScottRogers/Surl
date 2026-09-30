using Surl.Authentication;
using Surl.Cli;
using Surl.Kerberos;
using Surl.Output;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// The authentication policy the HTTP, MQTT, SMTP and SSH servers are given, as the command line asks for
/// it (ADR-0032, ADR-0051 section 6): the accounts from every <c>--user</c> and then the <c>--user-file</c>,
/// the public keys of every <c>--authorized-keys</c> file, the methods <c>--auth</c> accepts, and <c>--allow-anonymous</c> and
/// <c>--allow-plaintext-auth</c>; and the warning line each loosening option writes on start.
/// </summary>
internal static class AuthenticationComposition
{
    private const string WarningPrefix = "surl: warning: ";

    // The method each --auth word names (ADR-0032 section 3, as ADR-0049 section 3 grows it).
    // gssapi is refused as not available before the policy is composed.
    private static readonly Dictionary<string, AuthenticationMethod> MethodsByWord = new(StringComparer.Ordinal)
    {
        ["negotiate"] = AuthenticationMethod.Negotiate,
        ["ntlm"] = AuthenticationMethod.Ntlm,
        ["digest"] = AuthenticationMethod.Digest,
        ["digest-md5"] = AuthenticationMethod.DigestMd5,
        ["cram-md5"] = AuthenticationMethod.CramMd5,
        ["apop"] = AuthenticationMethod.Apop,
        ["basic"] = AuthenticationMethod.Basic,
        ["plain"] = AuthenticationMethod.Plain,
        ["login"] = AuthenticationMethod.Login,
        ["bearer"] = AuthenticationMethod.Bearer,
        ["oauthbearer"] = AuthenticationMethod.OAuthBearer,
        ["xoauth2"] = AuthenticationMethod.XOAuth2,
        ["external"] = AuthenticationMethod.External,
        ["aws-sigv4"] = AuthenticationMethod.AwsSigV4,
    };

    /// <summary>
    /// Builds the policy: reads the <c>--user-file</c>, then each <c>--authorized-keys</c> file, then
    /// the <c>--keytab</c> file through <paramref name="readFile"/> when given, and composes the
    /// Negotiate, NTLM, Basic, Bearer, Digest and AWS Signature Version 4 methods over the accounts;
    /// the SSH server checks its logins against the accounts and the authorized keys (ADR-0051,
    /// section 6). The keytab's Kerberos acceptor rides on the settings, read by no method yet
    /// (ADR-0057, decision 6).
    /// </summary>
    /// <param name="commandLine">The parsed command line.</param>
    /// <param name="readFile">Reads a file's bytes, given its path as given.</param>
    /// <param name="timeProvider">The clock the refusal delay, the Digest nonces, the Signature Version 4 window and the Kerberos acceptor run on.</param>
    /// <returns>
    /// The policy, every account's user name, the <c>--user</c> ones first, which the mail
    /// store's owners are (ADR-0050, decision 2), and the keytab entries skipped for their enctype;
    /// or, when it cannot be built, <see langword="null"/> with the exit code and the message after
    /// the <c>surl: </c> prefix (ADR-0032, sections 1 and 2; ADR-0051, section 6; ADR-0057, decision 1).
    /// </returns>
    public static (AuthenticationPolicy? Policy, IReadOnlyList<string> AccountNames, IReadOnlyList<KerberosKeytabSkippedEntry> SkippedKeytabEntries, SurlExitCode ExitCode, string? FailureMessage) Compose(
        SurlCommandLine commandLine, Func<string, byte[]> readFile, TimeProvider timeProvider)
    {
        var (accounts, exitCode, failureMessage) = ReadAccounts(commandLine, readFile);
        if (accounts is null)
        {
            return (null, [], [], exitCode, failureMessage);
        }

        var (authorizedKeys, keysExitCode, keysFailureMessage) = ReadAuthorizedKeys(commandLine, readFile);
        if (authorizedKeys is null)
        {
            return (null, [], [], keysExitCode, keysFailureMessage);
        }

        var (kerberosAcceptor, skippedKeytabEntries, keytabExitCode, keytabFailureMessage) = KeytabComposition.Read(commandLine, readFile, timeProvider);
        return keytabFailureMessage is not null
            ? (null, [], [], keytabExitCode, keytabFailureMessage)
            : (ComposePolicy(ComposeSettings(commandLine, accounts) with { AuthorizedKeys = authorizedKeys, KerberosAcceptor = kerberosAcceptor }, timeProvider),
                [.. accounts.Select(account => account.UserName)],
                skippedKeytabEntries,
                SurlExitCode.Ok,
                null);
    }

    /// <summary>
    /// Builds the policy of a command line with no accounts and no loosening option: what the
    /// servers are composed with when only their schemes are wanted.
    /// </summary>
    /// <param name="timeProvider">The clock the refusal delay, the Digest nonces and the Signature Version 4 window run on.</param>
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
                ? words.Select(word => MethodsByWord[word]).ToHashSet()
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

    // Every --authorized-keys file, in command-line order (ADR-0051, section 6): a file that
    // cannot be read is 37, a malformed line 2, each naming the file as given and never a key.
    private static (AuthorizedKeyBook? AuthorizedKeys, SurlExitCode ExitCode, string? FailureMessage) ReadAuthorizedKeys(
        SurlCommandLine commandLine, Func<string, byte[]> readFile)
    {
        List<AuthorizedKey> keys = [];
        foreach (var (userName, file) in commandLine.AuthorizedKeys)
        {
            byte[] content;
            try
            {
                content = readFile(file);
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                return (null, SurlExitCode.CouldNotReadFile, $"(37) Could not read authorized keys {file}");
            }

            var parsed = AuthorizedKeysParser.Parse(content, userName);
            if (parsed.Failure is { } lineFailure)
            {
                return (null, SurlExitCode.FailedInit, $"(2) Authorized keys {file}, {lineFailure.Describe()}");
            }

            keys.AddRange(parsed.Keys);
        }

        return (new AuthorizedKeyBook(keys), SurlExitCode.Ok, null);
    }

    private static AuthenticationPolicy ComposePolicy(AuthenticationSettings settings, TimeProvider timeProvider) =>
        new(
            settings,
            [
                new NegotiateAuthenticationMethod(settings.Accounts),
                new NtlmAuthenticationMethod(settings.Accounts),
                new BasicAuthenticationMethod(settings.Accounts),
                new BearerAuthenticationMethod(settings.Accounts),
                new DigestAuthenticationMethod(settings.Accounts, timeProvider),
                new AwsSigV4AuthenticationMethod(settings.Accounts, timeProvider),
            ],
            timeProvider);
}
