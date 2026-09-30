using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// Answers the FTP commands of one control connection, one line at a time, and holds the
/// session's state: the user name sent, whether the client is logged in, and the current
/// directory (ADR-0052, decisions 1 to 3).
/// </summary>
/// <remarks>
/// Every reply is fixed text from the table below; the one path a reply echoes, the current
/// directory in <c>257</c>, is rendered by <see cref="FtpPath.ToQuotedReplyText"/> (ADR-0006,
/// section 3). The commands a later task answers - data connections, downloads, listings,
/// uploads, file management and TLS - are answered <c>502 Command not implemented</c> until
/// then, which is true of this server now. It is not safe for concurrent calls.
/// </remarks>
internal sealed class FtpCommandResponder
{
    private const string CommandNotImplemented = "502 Command not implemented";
    private const string SyntaxError = "501 Syntax error in arguments";
    private const string DirectoryChanged = "250 Directory changed";

    // Answered before login as they are after it; every other command is 530 until then.
    private static readonly HashSet<string> CommandsBeforeLogin = new(StringComparer.Ordinal)
    {
        "USER", "PASS", "AUTH", "PBSZ", "PROT", "FEAT", "SYST", "HELP", "NOOP", "OPTS", "QUIT",
    };

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly ContentStore contentStore;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly Dictionary<string, Func<byte[]?, ValueTask<bool>>> commands;
    private readonly string[] recognizedCommands;
    private string? userName;
    private bool isLoggedIn;
    private IReadOnlyList<string> currentDirectory = [];

    /// <summary>
    /// Creates a responder for one control connection.
    /// </summary>
    /// <param name="connection">The control connection every reply is written to.</param>
    /// <param name="context">The exchange: its scheme, log and cancellation token.</param>
    /// <param name="contentStore">Where <c>CWD</c> looks for directories.</param>
    /// <param name="authenticationPolicy">Who may log in.</param>
    public FtpCommandResponder(
        IConnection connection, ExchangeContext context, ContentStore contentStore, IAuthenticationPolicy authenticationPolicy)
    {
        this.connection = connection;
        this.context = context;
        this.contentStore = contentStore;
        this.authenticationPolicy = authenticationPolicy;
        commands = new(StringComparer.Ordinal)
        {
            ["USER"] = AnswerUserAsync,
            ["PASS"] = AnswerPassAsync,
            ["PWD"] = _ => AnswerPrintWorkingDirectoryAsync(),
            ["XPWD"] = _ => AnswerPrintWorkingDirectoryAsync(),
            ["CWD"] = AnswerChangeWorkingDirectoryAsync,
            ["XCWD"] = AnswerChangeWorkingDirectoryAsync,
            ["CDUP"] = _ => AnswerChangeToParentDirectoryAsync(),
            ["XCUP"] = _ => AnswerChangeToParentDirectoryAsync(),
            ["TYPE"] = AnswerTypeAsync,
            ["MODE"] = argument => AnswerOneWordSettingAsync(argument, "S", "200 Mode set to S", "504 Mode not supported"),
            ["STRU"] = argument => AnswerOneWordSettingAsync(argument, "F", "200 Structure set to F", "504 Structure not supported"),
            ["SYST"] = _ => ReplyAsync("215 UNIX Type: L8"),
            ["FEAT"] = _ => ReplyAsync("211-Features:\r\n TVFS\r\n UTF8\r\n211 End"),
            ["OPTS"] = AnswerOptionsAsync,
            ["NOOP"] = _ => ReplyAsync("200 NOOP ok"),
            ["HELP"] = _ => AnswerHelpAsync(),
            ["ALLO"] = _ => ReplyAsync("202 Not needed"),
            ["ACCT"] = _ => ReplyAsync("202 Not needed"),
            ["QUIT"] = _ => AnswerQuitAsync(),
        };
        recognizedCommands = [.. commands.Keys.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Answers one command line.
    /// </summary>
    /// <param name="line">The line's bytes, without its line ending.</param>
    /// <returns><see langword="true"/> when the connection stays open for the next command; <see langword="false"/> after <c>QUIT</c>.</returns>
    public ValueTask<bool> AnswerAsync(byte[] line)
    {
        var commandLine = FtpCommandLine.Split(line);
        if (!isLoggedIn && !CommandsBeforeLogin.Contains(commandLine.Command))
        {
            return ReplyAsync("530 Please log in with USER and PASS");
        }

        return commands.TryGetValue(commandLine.Command, out var answer)
            ? answer(commandLine.Argument)
            : ReplyAsync(CommandNotImplemented);
    }

    // The name is never looked up here, so the reply cannot say whether an account exists
    // (ADR-0052, decision 3). A second USER before PASS replaces the name.
    private ValueTask<bool> AnswerUserAsync(byte[]? argument)
    {
        if (isLoggedIn)
        {
            return ReplyAsync("503 Already logged in");
        }

        if (argument is null)
        {
            return ReplyAsync(SyntaxError);
        }

        userName = Encoding.UTF8.GetString(argument);
        return ReplyAsync("331 Password required");
    }

    // The policy decides everything (ADR-0032, sections 5 and 6; ADR-0052, decision 3): the
    // server passes the name and the password's bytes as sent and the connection's TLS session
    // now. The name is spent either way, so a refused client starts again with USER.
    private ValueTask<bool> AnswerPassAsync(byte[]? argument)
    {
        if (isLoggedIn)
        {
            return ReplyAsync("503 Already logged in");
        }

        if (userName is not { } sentUserName)
        {
            return ReplyAsync("503 Send USER first");
        }

        userName = null;
        return LogInAsync(new PasswordLogin(context.Scheme, sentUserName, argument ?? [], connection.TlsSession));
    }

    private async ValueTask<bool> LogInAsync(PasswordLogin login)
    {
        var verdict = await authenticationPolicy.CheckPasswordLoginAsync(login, context.CancellationToken);
        NoteCheckedLogin(login, verdict);
        isLoggedIn = verdict is PasswordLoginVerdict.Accepted or PasswordLoginVerdict.AcceptedUnchecked;

        return await ReplyAsync(LoginReply(verdict));
    }

    private static string LoginReply(PasswordLoginVerdict verdict) => verdict switch
    {
        PasswordLoginVerdict.Accepted or PasswordLoginVerdict.AcceptedUnchecked => "230 Logged in",
        PasswordLoginVerdict.RefusedPlaintext => "530 Login needs TLS first: send AUTH TLS",
        _ => "530 Login incorrect",
    };

    // Only a checked login says what the credentials were worth (ADR-0032, section 8;
    // ADR-0038); a login refused or accepted unchecked is noted by its reply alone. The note
    // names the user, never the password.
    private void NoteCheckedLogin(PasswordLogin login, PasswordLoginVerdict verdict)
    {
        if (verdict is PasswordLoginVerdict.Accepted or PasswordLoginVerdict.RefusedCredentials)
        {
            context.Log.Note(new CheckedLogin(login.Scheme, login.UserName, verdict == PasswordLoginVerdict.Accepted).Note);
        }
    }

    private ValueTask<bool> AnswerPrintWorkingDirectoryAsync() =>
        ReplyAsync($"257 \"{FtpPath.ToQuotedReplyText(currentDirectory)}\" is the current directory");

    // A file, a missing path, a hidden one and anything under /.surl are all "No such
    // directory": the content store answers each as absent (ADR-0006 section 2, ADR-0031
    // decision 5).
    private ValueTask<bool> AnswerChangeWorkingDirectoryAsync(byte[]? argument)
    {
        if (argument is null)
        {
            return ReplyAsync(SyntaxError);
        }

        if (FtpPath.Resolve(currentDirectory, argument) is not { } directory || !IsDirectory(directory))
        {
            return ReplyAsync("550 No such directory");
        }

        currentDirectory = directory;
        return ReplyAsync(DirectoryChanged);
    }

    private bool IsDirectory(IReadOnlyList<string> path)
    {
        var mapping = contentStore.MapRequestPath(FtpPath.ToRequestPath(path));

        return mapping.IsMapped && contentStore.GetEntryKind(mapping) == ContentEntryKind.Directory;
    }

    private ValueTask<bool> AnswerChangeToParentDirectoryAsync()
    {
        currentDirectory = FtpPath.Parent(currentDirectory);
        return ReplyAsync(DirectoryChanged);
    }

    // Files are sent and stored as their bytes in either type (ADR-0052, decision 4).
    private ValueTask<bool> AnswerTypeAsync(byte[]? argument)
    {
        if (argument is null)
        {
            return ReplyAsync(SyntaxError);
        }

        return ReplyAsync(Encoding.ASCII.GetString(argument).ToUpperInvariant() switch
        {
            "I" or "L 8" => "200 Type set to I",
            "A" or "A N" => "200 Type set to A",
            _ => "504 Type not supported",
        });
    }

    private ValueTask<bool> AnswerOneWordSettingAsync(byte[]? argument, string supportedWord, string setReply, string unsupportedReply)
    {
        if (argument is null)
        {
            return ReplyAsync(SyntaxError);
        }

        return ReplyAsync(IsWord(argument, supportedWord) ? setReply : unsupportedReply);
    }

    private ValueTask<bool> AnswerOptionsAsync(byte[]? argument)
    {
        if (argument is null)
        {
            return ReplyAsync(SyntaxError);
        }

        return ReplyAsync(IsWord(argument, "UTF8 ON") ? "200 UTF8 set to on" : "501 Option not understood");
    }

    private ValueTask<bool> AnswerHelpAsync() =>
        ReplyAsync($"214-The following commands are recognized:\r\n {string.Join(' ', recognizedCommands)}\r\n214 End");

    private async ValueTask<bool> AnswerQuitAsync()
    {
        await ReplyAsync("221 Goodbye");
        await connection.CompleteWritesAsync(context.CancellationToken);

        return false;
    }

    private static bool IsWord(byte[] argument, string word) =>
        string.Equals(Encoding.ASCII.GetString(argument), word, StringComparison.OrdinalIgnoreCase);

    private async ValueTask<bool> ReplyAsync(string reply)
    {
        await connection.WriteAsync(Encoding.ASCII.GetBytes(reply + "\r\n"), context.CancellationToken);

        return true;
    }
}
