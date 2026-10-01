using System.Globalization;
using System.Text;
using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Ftp;

/// <summary>
/// Answers the FTP commands of one control connection, one line at a time, and holds the
/// session's state: the user name sent, whether the client is logged in, the current
/// directory, the <c>REST</c> offset, the entry an <c>RNFR</c> named and the data connection
/// prepared (ADR-0052, decisions 1 to 4 and 6 to 8).
/// </summary>
/// <remarks>
/// Every reply is fixed text from the table below; the paths a reply echoes - the current
/// directory and a created directory in <c>257</c>, the file in <c>150</c>, the word of a
/// <c>SITE</c> command and the entry <c>MLST</c> describes - are rendered by
/// <see cref="FtpPath.ToQuotedReplyText"/> and <see cref="FtpPath.ToReplyText(byte[])"/>
/// (ADR-0006, section 3); a listing sent over a data connection names each entry in UTF-8, as
/// <c>FEAT</c>'s <c>UTF8</c> says. TLS is <c>AUTH TLS</c> (or <c>AUTH SSL</c>), <c>PBSZ</c> and
/// <c>PROT</c>, with <c>CCC</c> always refused (ADR-0052, decision 5). It is not safe for
/// concurrent calls.
/// </remarks>
internal sealed class FtpCommandResponder : IAsyncDisposable
{
    private const string AuthFeatures = " AUTH TLS\r\n PBSZ\r\n PROT\r\n";
    private const string CommandNotImplemented = "502 Command not implemented";
    private const string SyntaxError = "501 Syntax error in arguments";
    private const string DirectoryChanged = "250 Directory changed";
    private const string NoSuchFile = "550 No such file";
    private const string TransferComplete = "226 Transfer complete";
    private const string TransferAborted = "426 Connection closed; transfer aborted";
    private const string FileUnreadable = "451 Cannot read the file";
    private const string NoSuchDirectory = "550 No such directory";
    private const string NotPermitted = "550 Not permitted";
    private const string NoSuchParentDirectory = "553 No such directory";
    private const string UsePassiveOrPortFirst = "425 Use PASV or PORT first";

    // Answered before login as they are after it; every other command is 530 until then.
    private static readonly HashSet<string> CommandsBeforeLogin = new(StringComparer.Ordinal)
    {
        "USER", "PASS", "AUTH", "PBSZ", "PROT", "FEAT", "SYST", "HELP", "NOOP", "OPTS", "QUIT",
    };

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly FtpLineReader reader;
    private readonly ContentStore contentStore;
    private readonly IAuthenticationPolicy authenticationPolicy;
    private readonly bool isTlsUpgradeAvailable;
    private readonly Dictionary<string, Func<byte[]?, ValueTask<bool>>> commands;
    private readonly string[] recognizedCommands;
    private readonly FtpDataConnections dataConnections;
    private string? userName;
    private bool isLoggedIn;
    private bool isProtectionBufferSizeSet;
    private IReadOnlyList<string> currentDirectory = [];
    private long restartOffset;
    private IReadOnlyList<string>? renameSource;
    private IReadOnlyList<string>? renameSourceFromPreviousCommand;

    /// <summary>
    /// Creates a responder for one control connection.
    /// </summary>
    /// <param name="connection">The control connection every reply is written to.</param>
    /// <param name="context">The exchange: its scheme, data-connection opener, limits, log and cancellation token.</param>
    /// <param name="reader">The control connection's line reader, whose buffered bytes <c>AUTH</c> throws away.</param>
    /// <param name="contentStore">Where <c>CWD</c> looks for directories and <c>RETR</c> for files.</param>
    /// <param name="authenticationPolicy">Who may log in.</param>
    /// <param name="isTlsUpgradeAvailable">Whether the listener has a certificate, so <c>AUTH</c> can upgrade the connection.</param>
    public FtpCommandResponder(
        IConnection connection,
        ExchangeContext context,
        FtpLineReader reader,
        ContentStore contentStore,
        IAuthenticationPolicy authenticationPolicy,
        bool isTlsUpgradeAvailable)
    {
        this.connection = connection;
        this.context = context;
        this.reader = reader;
        this.contentStore = contentStore;
        this.authenticationPolicy = authenticationPolicy;
        this.isTlsUpgradeAvailable = isTlsUpgradeAvailable;

        // Data connections are private from the start on ftps:// and clear on ftp:// until PROT
        // says otherwise (ADR-0052, decision 5).
        dataConnections = new FtpDataConnections(connection, context)
        {
            IsProtected = string.Equals(context.Scheme, "ftps", StringComparison.OrdinalIgnoreCase),
        };
        commands = new(StringComparer.Ordinal)
        {
            ["USER"] = AnswerUserAsync,
            ["PASS"] = AnswerPassAsync,
            ["AUTH"] = AnswerAuthAsync,
            ["PBSZ"] = AnswerProtectionBufferSizeAsync,
            ["PROT"] = AnswerProtectionLevelAsync,
            ["CCC"] = _ => ReplyAsync("534 Request denied for policy reasons"),
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
            ["FEAT"] = _ => ReplyAsync($"211-Features:\r\n EPRT\r\n EPSV\r\n MDTM\r\n MLST type*;size*;modify*;\r\n PASV\r\n REST STREAM\r\n SIZE\r\n TVFS\r\n UTF8\r\n{(isTlsUpgradeAvailable ? AuthFeatures : "")}211 End"),
            ["EPSV"] = async argument => await ReplyAsync(await dataConnections.AnswerExtendedPassiveAsync(argument)),
            ["PASV"] = async _ => await ReplyAsync(await dataConnections.AnswerPassiveAsync()),
            ["EPRT"] = async argument => await ReplyAsync(await dataConnections.AnswerActiveAsync(argument, isExtended: true)),
            ["PORT"] = async argument => await ReplyAsync(await dataConnections.AnswerActiveAsync(argument, isExtended: false)),
            ["SIZE"] = AnswerSizeAsync,
            ["MDTM"] = AnswerModificationTimeAsync,
            ["REST"] = AnswerRestartAsync,
            ["RETR"] = AnswerRetrieveAsync,
            ["LIST"] = argument => AnswerListingAsync(WithoutLsOptions(argument), FtpListingFormat.LongLine, listsOneFile: true),
            ["NLST"] = argument => AnswerListingAsync(WithoutLsOptions(argument), FtpListingFormat.NameLine, listsOneFile: true),
            ["MLSD"] = argument => AnswerListingAsync(argument, FtpListingFormat.FactsLine, listsOneFile: false),
            ["MLST"] = AnswerMachineListEntryAsync,
            ["STOR"] = argument => AnswerUploadAsync(argument, appends: false),
            ["APPE"] = argument => AnswerUploadAsync(argument, appends: true),
            ["MKD"] = argument => AnswerWriteAsync(argument, AnswerMakeDirectory),
            ["XMKD"] = argument => AnswerWriteAsync(argument, AnswerMakeDirectory),
            ["RMD"] = argument => AnswerWriteAsync(argument, AnswerRemoveDirectory),
            ["XRMD"] = argument => AnswerWriteAsync(argument, AnswerRemoveDirectory),
            ["DELE"] = argument => AnswerWriteAsync(argument, AnswerDelete),
            ["RNFR"] = argument => AnswerWriteAsync(argument, AnswerRenameFrom),
            ["RNTO"] = argument => AnswerWriteAsync(argument, AnswerRenameTo),
            ["SITE"] = argument => AnswerWriteAsync(argument, AnswerSite),
            ["ABOR"] = _ => ReplyAsync("226 Abort successful"),
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

        // An RNTO renames only what the command immediately before it named (ADR-0052, decision 8).
        renameSourceFromPreviousCommand = renameSource;
        renameSource = null;
        if (!isLoggedIn && !CommandsBeforeLogin.Contains(commandLine.Command))
        {
            return ReplyAsync("530 Please log in with USER and PASS");
        }

        return commands.TryGetValue(commandLine.Command, out var answer)
            ? answer(commandLine.Argument)
            : ReplyAsync(CommandNotImplemented);
    }

    /// <summary>
    /// Disposes the passive data listener, if one is still waiting for curl.
    /// </summary>
    /// <returns>A task that completes when it is disposed.</returns>
    public ValueTask DisposeAsync() => dataConnections.DisposeAsync();

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

    // AUTH TLS and AUTH SSL (curl sends SSL first): 234, then every byte pipelined after the
    // AUTH line thrown away unrun, then the handshake (ADR-0010, section 1; ADR-0052, decision
    // 5). A failed handshake throws TlsHandshakeException, which the engine notes.
    private async ValueTask<bool> AnswerAuthAsync(byte[]? argument)
    {
        if (AuthRefusal(argument) is { } refusal)
        {
            return await ReplyAsync(refusal);
        }

        await ReplyAsync("234 AUTH accepted, start TLS");
        var discarded = reader.DiscardBuffered();
        if (discarded > 0)
        {
            context.Log.Note($"Discarded {discarded} bytes sent after AUTH");
        }

        await connection.UpgradeToTlsAsync(context.CancellationToken);
        return true;
    }

    private string? AuthRefusal(byte[]? argument) =>
        argument is null ? SyntaxError
        : connection.TlsSession is not null ? "503 Already using TLS"
        : !IsWord(argument, "TLS") && !IsWord(argument, "SSL") ? "504 Security mechanism not understood"
        : !isTlsUpgradeAvailable ? "534 TLS is not available"
        : null;

    // Only a buffer size of 0 exists under TLS, so any size is answered PBSZ=0 (RFC 4217,
    // section 9); it needs TLS first, from AUTH or from ftps://.
    private ValueTask<bool> AnswerProtectionBufferSizeAsync(byte[]? argument)
    {
        if (argument is null)
        {
            return ReplyAsync(SyntaxError);
        }

        if (connection.TlsSession is null)
        {
            return ReplyAsync("503 Send AUTH first");
        }

        isProtectionBufferSizeSet = true;
        return ReplyAsync("200 PBSZ=0");
    }

    // PROT P makes every later data connection TLS and PROT C plaintext; S and E have no TLS
    // meaning (RFC 4217, section 9).
    private ValueTask<bool> AnswerProtectionLevelAsync(byte[]? argument)
    {
        if (argument is null)
        {
            return ReplyAsync(SyntaxError);
        }

        if (!isProtectionBufferSizeSet)
        {
            return ReplyAsync("503 Send PBSZ first");
        }

        var level = Encoding.ASCII.GetString(argument).ToUpperInvariant();
        switch (level)
        {
            case "C" or "P":
                dataConnections.IsProtected = level == "P";
                return ReplyAsync($"200 Protection level set to {level}");
            case "S" or "E":
                return ReplyAsync("536 Protection level not supported");
            default:
                return ReplyAsync("504 Protection level not understood");
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

    private ValueTask<bool> AnswerSizeAsync(byte[]? argument) =>
        AnswerFileStatusAsync(argument, status => $"213 {status.Length}");

    // The file's last write in UTC (RFC 3659, section 3), never the clock's time.
    private ValueTask<bool> AnswerModificationTimeAsync(byte[]? argument) =>
        AnswerFileStatusAsync(argument, status => $"213 {status.LastModifiedUtc.UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}");

    private ValueTask<bool> AnswerFileStatusAsync(byte[]? argument, Func<ContentFileStatus, string> reply)
    {
        if (argument is null)
        {
            return ReplyAsync(SyntaxError);
        }

        return ReplyAsync(FindFile(argument) is { } file ? reply(file.Status) : NoSuchFile);
    }

    private ValueTask<bool> AnswerRestartAsync(byte[]? argument)
    {
        if (argument is null)
        {
            return ReplyAsync(SyntaxError);
        }

        if (!long.TryParse(Encoding.Latin1.GetString(argument), NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
        {
            return ReplyAsync("501 Invalid restart offset");
        }

        restartOffset = offset;
        return ReplyAsync($"350 Restarting at {offset}");
    }

    // Everything that can refuse the download is checked before a data connection is opened,
    // and the data connection is opened before 150, so 150 always means the bytes follow
    // (ADR-0052, decisions 4 and 6). The REST offset applies to this RETR alone.
    private async ValueTask<bool> AnswerRetrieveAsync(byte[]? argument)
    {
        var offset = restartOffset;
        restartOffset = 0;
        if (argument is null)
        {
            return await ReplyAsync(SyntaxError);
        }

        if (FindFile(argument) is not { } file)
        {
            return await ReplyAsync(NoSuchFile);
        }

        if (offset > file.Status.Length)
        {
            return await ReplyAsync("554 Restart offset past end of file");
        }

        var byteCount = file.Status.Length - offset;
        return await TransferAsync(
            $"150 Opening data connection for {FtpPath.ToReplyText(argument)} ({byteCount} bytes)",
            dataConnection => SendFileAsync(dataConnection, file.Mapping, offset, byteCount));
    }

    // The data connection is opened before 150, so 150 always means the bytes follow; the reply
    // send returns follows them. Each data connection carries one transfer (ADR-0052, decision 6).
    // Under PROT P the TLS handshake runs after 150, the order pinned upstream curl completed
    // against the recorder, and a failed one is 425 in place of the transfer.
    private async ValueTask<bool> TransferAsync(string openingReply, Func<IConnection, Task<string>> send)
    {
        if (!dataConnections.IsPrepared)
        {
            return await ReplyAsync(UsePassiveOrPortFirst);
        }

        if (await dataConnections.OpenAsync() is not { } dataConnection)
        {
            return await ReplyAsync(FtpDataConnections.CannotOpenDataConnection);
        }

        await using (dataConnection)
        {
            await ReplyAsync(openingReply);
            return await ReplyAsync(await dataConnections.ProtectAsync(dataConnection)
                ? await send(dataConnection)
                : FtpDataConnections.CannotOpenDataConnection);
        }
    }

    // LIST, NLST and MLSD of a directory are directory listings, answered only with
    // --list-directories and otherwise exactly as a missing directory, before any data
    // connection is used (ADR-0006, section 2; ADR-0052, decision 7). LIST and NLST naming one
    // exposed file list that file whatever the switch says: that is not a directory listing.
    private ValueTask<bool> AnswerListingAsync(
        byte[]? argument, Func<ContentDirectoryEntry, DateTimeOffset, string> formatLine, bool listsOneFile)
    {
        var path = argument is null ? currentDirectory : FtpPath.Resolve(currentDirectory, argument);
        if (path is null || FindListedEntries(path, listsOneFile) is not { } entries)
        {
            return ReplyAsync(NoSuchDirectory);
        }

        var listing = FtpListingFormat.Encode(entries, formatLine, context.TimeProvider.GetUtcNow());
        return TransferAsync("150 Opening data connection for directory listing", dataConnection => SendListingAsync(dataConnection, listing));
    }

    // A directory's entries as the content store lists them (dot-files left out unless
    // --serve-dot-files, /.surl never), or one file's entry, or nothing when the path is
    // neither or cannot be read (ADR-0009; ADR-0023).
    private IReadOnlyList<ContentDirectoryEntry>? FindListedEntries(IReadOnlyList<string> path, bool listsOneFile)
    {
        var mapping = contentStore.MapRequestPath(FtpPath.ToRequestPath(path));
        if (!mapping.IsMapped)
        {
            return null;
        }

        try
        {
            var listing = contentStore.ListDirectory(mapping, context.CancellationToken);
            return listing.IsListed ? listing.Entries
                : listsOneFile && contentStore.GetFileStatus(mapping) is { } status ? [FileEntry(path, status)]
                : null;
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            context.Log.Note($"{mapping.Location} could not be listed ({exception.GetType().Name}: {exception.Message}); answered 550.");
            return null;
        }
    }

    private static ContentDirectoryEntry FileEntry(IReadOnlyList<string> path, ContentFileStatus status) =>
        new(path[^1], ContentEntryKind.File, status.Length, status.LastModifiedUtc);

    // curl closing the data connection before the listing is all sent surfaces as a failed
    // write, answered 426 with the data connection reset, as for RETR.
    private async Task<string> SendListingAsync(IConnection dataConnection, byte[] listing)
    {
        try
        {
            await dataConnection.WriteAsync(listing, context.CancellationToken);
            await dataConnection.CompleteWritesAsync(context.CancellationToken);
            return TransferComplete;
        }
        catch (IOException)
        {
            dataConnection.Abort();
            return NoteFailedTransfer(TransferAborted, "The client closed the data connection before the whole listing was sent; the data connection was reset.");
        }
    }

    // Arguments LIST and NLST take the way ls does, such as "-a" or "-la", are ignored
    // (ADR-0052, decision 7): each leading word that starts with '-' is dropped, and what
    // follows, if anything, is the path.
    private static byte[]? WithoutLsOptions(byte[]? argument)
    {
        while (argument is [(byte)'-', ..])
        {
            var space = Array.IndexOf(argument, (byte)' ');
            argument = space < 0 ? null : argument[(space + 1)..];
        }

        return argument;
    }

    // MLST describes one entry on the control connection, a directory included: it is not a
    // directory listing, so it is answered whatever --list-directories says (ADR-0052,
    // decisions 1 and 7).
    private ValueTask<bool> AnswerMachineListEntryAsync(byte[]? argument)
    {
        var path = argument is null ? currentDirectory : FtpPath.Resolve(currentDirectory, argument);
        if (path is null || DescribeEntry(path) is not { } facts)
        {
            return ReplyAsync(NoSuchFile);
        }

        var pathText = FtpPath.ToReplyText(path);
        return ReplyAsync($"250-Listing {pathText}\r\n {facts} {pathText}\r\n250 End");
    }

    // A file's facts come from its status; a directory's from its entry in its parent's
    // listing, or type=dir alone for / or a directory its parent does not list by that name.
    private string? DescribeEntry(IReadOnlyList<string> path)
    {
        var mapping = contentStore.MapRequestPath(FtpPath.ToRequestPath(path));
        if (!mapping.IsMapped)
        {
            return null;
        }

        try
        {
            return contentStore.GetFileStatus(mapping) is { } status ? FtpListingFormat.Facts(FileEntry(path, status))
                : contentStore.GetEntryKind(mapping) == ContentEntryKind.Directory ? DirectoryFacts(path)
                : null;
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            context.Log.Note($"{mapping.Location} could not be read ({exception.GetType().Name}: {exception.Message}); answered 550.");
            return null;
        }
    }

    private string DirectoryFacts(IReadOnlyList<string> path)
    {
        if (path.Count == 0)
        {
            return "type=dir;";
        }

        var parent = contentStore.MapRequestPath(FtpPath.ToRequestPath(FtpPath.Parent(path)));
        var entry = contentStore.ListDirectoryWhateverTheListingSwitchSays(parent, context.CancellationToken).Entries
            .FirstOrDefault(candidate => candidate.Kind == ContentEntryKind.Directory && string.Equals(candidate.Name, path[^1], StringComparison.Ordinal));

        return entry is null ? "type=dir;" : FtpListingFormat.Facts(entry);
    }

    // curl closing the data connection early (a range) surfaces as a failed write; the file
    // failing to read surfaces as an exception with no failed write. The first is 426, the
    // second 451, and either way the data connection is reset so curl never takes a short
    // file for a whole one.
    private async Task<string> SendFileAsync(IConnection dataConnection, ContentPathMapping mapping, long offset, long byteCount)
    {
        var destination = new DataConnectionWriteStream(dataConnection);
        var fileRead = false;
        try
        {
            if (byteCount > 0)
            {
                var range = ContentByteRange.Select(offset + byteCount, offset, offset + byteCount - 1);
                await contentStore.CopyFileBytesAsync(mapping, range, destination, context.CancellationToken);
            }

            fileRead = true;
            await dataConnection.CompleteWritesAsync(context.CancellationToken);
            return TransferComplete;
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            dataConnection.Abort();
            return fileRead || destination.ConnectionWriteFailed
                ? NoteFailedTransfer(TransferAborted, "The client closed the data connection before the whole file was sent; the data connection was reset.")
                : NoteFailedTransfer(FileUnreadable, $"{mapping.Location} could not be read after 150 was sent ({exception.GetType().Name}: {exception.Message}); the data connection was reset.");
        }
    }

    private string NoteFailedTransfer(string reply, string note)
    {
        context.Log.Note(note);
        return reply;
    }

    // A path that does not resolve, is refused, is hidden, is a directory or holds nothing is
    // not a file (ADR-0006, section 2; ADR-0031, decision 5; ADR-0052, decision 2), and nor is
    // one whose status cannot be read: a peer cannot tell it from a missing file (ADR-0023).
    private (ContentPathMapping Mapping, ContentFileStatus Status)? FindFile(byte[] argument)
    {
        if (FtpPath.Resolve(currentDirectory, argument) is not { } path)
        {
            return null;
        }

        var mapping = contentStore.MapRequestPath(FtpPath.ToRequestPath(path));
        try
        {
            return mapping.IsMapped && contentStore.GetFileStatus(mapping) is { } status ? (mapping, status) : null;
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            context.Log.Note($"{mapping.Location} could not be read ({exception.GetType().Name}: {exception.Message}); answered 550.");
            return null;
        }
    }

    // STOR and APPE (ADR-0052, decision 8). A REST offset applies to this command alone: one
    // equal to the file's length appends, 0 writes afresh, anything else is refused.
    private async ValueTask<bool> AnswerUploadAsync(byte[]? argument, bool appends)
    {
        var offset = restartOffset;
        restartOffset = 0;
        if (argument is null)
        {
            return await ReplyAsync(SyntaxError);
        }

        var target = ResolveMapped(argument);
        if (FindUploadRefusal(target, argument, offset) is { } refusal)
        {
            return await ReplyAsync(refusal);
        }

        return await ReplyAsync(await ReceiveUploadAsync(target!.Value.Mapping, appends || offset > 0, argument));
    }

    // Everything the server can see is checked before a data connection is used; what only the
    // content store can see (a hidden new name, a directory in the way) it refuses before it
    // reads a byte, so before the data connection is opened too.
    private string? FindUploadRefusal((IReadOnlyList<string> Path, ContentPathMapping Mapping)? target, byte[] argument, long offset)
    {
        if (!contentStore.ExposureOptions.AllowUploads || target is not { } resolved)
        {
            return NotPermitted;
        }

        return IsDirectory(FtpPath.Parent(resolved.Path)) ? FindRestartOrDataConnectionRefusal(argument, offset) : NoSuchParentDirectory;
    }

    private string? FindRestartOrDataConnectionRefusal(byte[] argument, long offset)
    {
        if (offset > 0 && !IsFileOfLength(argument, offset))
        {
            return "554 Restart offset must equal the file's length";
        }

        return dataConnections.IsPrepared ? null : UsePassiveOrPortFirst;
    }

    private bool IsFileOfLength(byte[] argument, long length) =>
        FindFile(argument) is { } file && file.Status.Length == length;

    // The upload is written through the content store's temporary dot-file and renamed into
    // place only once all of it has arrived, so every failure leaves nothing behind (ADR-0006,
    // section 5). A data connection that is refused an upload, or breaks, is reset.
    private async Task<string> ReceiveUploadAsync(ContentPathMapping mapping, bool appends, byte[] argument)
    {
        await using var upload = new DataConnectionUploadStream(
            dataConnections, async () => await ReplyAsync($"150 Opening data connection for {FtpPath.ToReplyText(argument)}"));
        try
        {
            var result = appends
                ? await contentStore.AppendUploadAsync(mapping, upload, context.CancellationToken)
                : await contentStore.WriteUploadAsync(mapping, upload, context.CancellationToken);
            return AnswerUploadResult(result, upload);
        }
        catch (FtpDataConnectionNotOpenedException)
        {
            return FtpDataConnections.CannotOpenDataConnection;
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            upload.AbortConnection();
            return upload.ConnectionReadFailed
                ? NoteFailedTransfer(TransferAborted, "The data connection failed before the whole upload arrived; the partial upload was deleted and the data connection was reset.")
                : NoteFailedTransfer("451 Cannot write the file", $"{mapping.Location} could not be written ({exception.GetType().Name}: {exception.Message}); the partial upload was deleted and the data connection was reset.");
        }
    }

    private string AnswerUploadResult(ContentUploadResult result, DataConnectionUploadStream upload)
    {
        switch (result)
        {
            case ContentUploadResult.Written:
                return TransferComplete;
            case ContentUploadResult.TooLarge:
                upload.AbortConnection();
                return NoteFailedTransfer("552 Upload exceeds the size limit", $"The upload grew past the limit of {contentStore.ExposureOptions.MaxUploadBytes} bytes; the partial upload was deleted and the data connection was reset.");
            default:
                return NotPermitted;
        }
    }

    // Every file-management command needs --allow-uploads (ADR-0006, section 2; ADR-0052,
    // decision 8). A file system that fails the change is answered 451 and noted (ADR-0023).
    private ValueTask<bool> AnswerWriteAsync(byte[]? argument, Func<byte[], string> answer)
    {
        if (argument is null)
        {
            return ReplyAsync(SyntaxError);
        }

        if (!contentStore.ExposureOptions.AllowUploads)
        {
            return ReplyAsync(NotPermitted);
        }

        try
        {
            return ReplyAsync(answer(argument));
        }
        catch (Exception exception) when (IsFileSystemFailure(exception))
        {
            return ReplyAsync(NoteFailedTransfer("451 The change could not be made", $"A file-management command failed ({exception.GetType().Name}: {exception.Message}); answered 451."));
        }
    }

    private string AnswerMakeDirectory(byte[] argument)
    {
        if (ResolveMapped(argument) is not { } target)
        {
            return NotPermitted;
        }

        return contentStore.CreateDirectory(target.Mapping) switch
        {
            ContentChangeResult.Done => $"257 \"{FtpPath.ToQuotedReplyText(target.Path)}\" created",
            ContentChangeResult.Exists => "550 Already exists",
            ContentChangeResult.NoSuchDirectory => NoSuchDirectory,
            _ => NotPermitted,
        };
    }

    private string AnswerRemoveDirectory(byte[] argument)
    {
        if (ResolveMapped(argument) is not { } target)
        {
            return NoSuchDirectory;
        }

        return contentStore.RemoveEmptyDirectory(target.Mapping) switch
        {
            ContentChangeResult.Done => "250 Directory removed",
            ContentChangeResult.NotEmpty => "550 Directory not empty",
            ContentChangeResult.Absent => NoSuchDirectory,
            _ => NotPermitted,
        };
    }

    private string AnswerDelete(byte[] argument) =>
        ResolveMapped(argument) is { } target && contentStore.DeleteFile(target.Mapping) == ContentChangeResult.Done
            ? "250 File deleted"
            : NoSuchFile;

    private string AnswerRenameFrom(byte[] argument)
    {
        if (ResolveMapped(argument) is not { } target || contentStore.GetEntryKind(target.Mapping) == ContentEntryKind.None)
        {
            return NoSuchFile;
        }

        renameSource = target.Path;
        return "350 Ready for RNTO";
    }

    // A file already at the new name is replaced; a directory there, or a file there when a
    // directory is renamed, is in the way. A source gone since RNFR is answered as not
    // permitted, as any other refusal the content store makes.
    private string AnswerRenameTo(byte[] argument)
    {
        if (renameSourceFromPreviousCommand is not { } sourcePath)
        {
            return "503 Send RNFR first";
        }

        if (ResolveMapped(argument) is not { } destination)
        {
            return NotPermitted;
        }

        var source = contentStore.MapRequestPath(FtpPath.ToRequestPath(sourcePath));
        var result = contentStore.RenameEntry(source, destination.Mapping);
        return result == ContentChangeResult.Exists ? InTheWayOfARenameReply(destination.Mapping) : RenameReply(result);
    }

    private string InTheWayOfARenameReply(ContentPathMapping destination) =>
        contentStore.GetEntryKind(destination) == ContentEntryKind.Directory
            ? "553 Cannot rename onto a directory"
            : "553 Cannot rename a directory onto a file";

    private static string RenameReply(ContentChangeResult result) => result switch
    {
        ContentChangeResult.Done => "250 Renamed",
        ContentChangeResult.NoSuchDirectory => NoSuchParentDirectory,
        _ => NotPermitted,
    };

    // The content store has no permissions, owners or times a SITE form could set, so every
    // form is refused, naming the word the client sent.
    private static string AnswerSite(byte[] argument)
    {
        var space = Array.IndexOf(argument, (byte)' ');
        return $"504 SITE {FtpPath.ToReplyText(space < 0 ? argument : argument[..space])} is not supported";
    }

    // A path that does not resolve (not UTF-8, above /) or that the content store refuses has
    // no mapping; a hidden one is mapped, and the content store answers it as absent.
    private (IReadOnlyList<string> Path, ContentPathMapping Mapping)? ResolveMapped(byte[] argument)
    {
        if (FtpPath.Resolve(currentDirectory, argument) is not { } path)
        {
            return null;
        }

        var mapping = contentStore.MapRequestPath(FtpPath.ToRequestPath(path));
        return mapping.IsMapped ? (path, mapping) : null;
    }

    private static bool IsFileSystemFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

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
