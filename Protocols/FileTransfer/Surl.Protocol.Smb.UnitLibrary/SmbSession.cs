using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smb;

/// <summary>
/// One connection's SMB version 1 session: the negotiate, the NTLMv1 session setup checked
/// through <see cref="ISmbAuthenticationPolicy"/>, the trees connected to shares and the files
/// opened on them for reading or for writing, answering each decoded request as ADR-0073
/// decisions 1 to 5 say.
/// </summary>
/// <remarks>
/// Disposing the session discards every upload still open, as a connection that ends with one
/// open must. Not safe for concurrent calls.
/// </remarks>
internal sealed class SmbSession : IAsyncDisposable
{
    /// <summary>The UID of the connection's one session (ADR-0073, decision 2).</summary>
    public const ushort UserId = 1;

    /// <summary>The most trees one session may have connected at once (ADR-0073, decision 2).</summary>
    public const int MaxTrees = 16;

    /// <summary>The most files one session may have open at once (ADR-0073, decision 2).</summary>
    public const int MaxOpenFiles = 16;

    /// <summary>The most bytes one read answers, so the response's byte count stays under 65535 (ADR-0073, decision 5).</summary>
    public const int MaxReadBytes = 61440;

    /// <summary>The dialect the server speaks, the only one curl offers.</summary>
    public const string Dialect = "NT LM 0.12";

    /// <summary>The domain the negotiate and session setup responses name (ADR-0073, decision 1).</summary>
    public const string DomainName = "SURL";

    /// <summary>The largest SMB message a NetBIOS frame's 17-bit length can announce.</summary>
    public const int LargestMessageLength = 0x1FFFF;

    private const int ChallengeLength = 8;
    private const byte UserLevelChallengeResponseSecurity = 0x03;
    private const uint LargeFilesAndNtSmbsCapabilities = 0x00000018;
    private const uint MaxRawSize = 65536;
    private const ushort LoggedOnAsUser = 0;
    private const ushort LoggedOnAsGuest = 1;
    private const string DiskShareService = "A:";
    private const string InterProcessCommunicationShare = "IPC$";
    private const uint FileOpenDisposition = 1;
    private const uint FileOpened = 1;
    private const uint FileCreated = 2;
    private const uint FileOverwritten = 3;
    private const uint NormalFileAttributes = 0x80;

    // FILE_OVERWRITE_IF, what curl asks for: a missing file is created, an existing one replaced.
    private static readonly ContentUploadOpening OverwriteIfOpening = new(StartsFromExistingBytes: false, CreatesMissingFile: true, RefusesExistingFile: false);

    // GENERIC_ALL, GENERIC_WRITE, FILE_APPEND_DATA and FILE_WRITE_DATA.
    private const uint WriteAccessMask = 0x50000006;

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly ContentStore contentStore;
    private readonly ISmbAuthenticationPolicy authenticationPolicy;
    private readonly ISmbChallengeSource challengeSource;
    private readonly Dictionary<ushort, string> sharesByTreeId = [];
    private readonly Dictionary<ushort, SmbOpenFile> openFilesByFileId = [];
    private byte[]? challenge;
    private bool isLoggedIn;

    /// <summary>
    /// Creates the session of one connection.
    /// </summary>
    /// <param name="connection">The connection, whose TLS session a login is told about.</param>
    /// <param name="context">The exchange: its log, clock, limits and cancellation.</param>
    /// <param name="contentStore">The content store whose top-level directories are the shares.</param>
    /// <param name="authenticationPolicy">Who may log in.</param>
    /// <param name="challengeSource">Where the negotiate response's challenge comes from.</param>
    public SmbSession(
        IConnection connection,
        ExchangeContext context,
        ContentStore contentStore,
        ISmbAuthenticationPolicy authenticationPolicy,
        ISmbChallengeSource challengeSource)
    {
        this.connection = connection;
        this.context = context;
        this.contentStore = contentStore;
        this.authenticationPolicy = authenticationPolicy;
        this.challengeSource = challengeSource;
    }

    /// <summary>
    /// Answers one decoded request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The response, and whether the connection closes after it.</returns>
    public async ValueTask<SmbAnswer> AnswerAsync(SmbRequest request) => request switch
    {
        SmbNegotiateRequest negotiate => AnswerNegotiate(negotiate),
        SmbSessionSetupRequest sessionSetup => await AnswerSessionSetupAsync(sessionSetup),
        _ when !isLoggedIn || request.Header.UserId != UserId => Refuse(request.Header, SmbStatus.BadUserId),
        _ => await AnswerLoggedInAsync(request),
    };

    private async ValueTask<SmbAnswer> AnswerLoggedInAsync(SmbRequest request) => request switch
    {
        SmbTreeConnectRequest treeConnect => AnswerTreeConnect(treeConnect),
        _ when !sharesByTreeId.ContainsKey(request.Header.TreeId) => Refuse(request.Header, SmbStatus.InvalidTreeId),
        _ => await AnswerOnTreeAsync(request),
    };

    private async ValueTask<SmbAnswer> AnswerOnTreeAsync(SmbRequest request) => request switch
    {
        SmbTreeDisconnectRequest treeDisconnect => await AnswerTreeDisconnectAsync(treeDisconnect),
        SmbNtCreateRequest ntCreate => await AnswerNtCreateAsync(ntCreate),
        SmbReadRequest read => await AnswerReadAsync(read),
        SmbCloseRequest close => await AnswerCloseAsync(close),
        // Negotiate and session setup were answered before a login was needed: a write is all that is left.
        _ => await AnswerWriteAsync((SmbWriteRequest)request),
    };

    /// <summary>
    /// Discards every upload still open: a connection that ends with one open commits nothing
    /// (ADR-0073, decision 5).
    /// </summary>
    /// <returns>A task that completes when every open upload is discarded.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (var openFile in openFilesByFileId.Values)
        {
            await DiscardUploadAsync(openFile);
        }

        openFilesByFileId.Clear();
    }

    private static async ValueTask DiscardUploadAsync(SmbOpenFile openFile)
    {
        if (openFile.Upload is { } upload)
        {
            await upload.DisposeAsync();
        }
    }

    /// <summary>
    /// Answers a message that did not decode into a request: <c>ERRSRV/ERRsmbcmd</c> for a
    /// command the server does not serve, <c>ERRSRV/ERRerror</c> for anything malformed.
    /// </summary>
    /// <param name="fault">Why the message did not decode.</param>
    /// <param name="header">The message's header.</param>
    /// <returns>The error response; the connection stays open.</returns>
    public SmbAnswer AnswerFault(SmbRequestFault fault, SmbHeader header)
    {
        if (fault != SmbRequestFault.UnsupportedCommand)
        {
            return Refuse(header, SmbStatus.ServerError);
        }

        context.Log.Note($"SMB command 0x{header.Command:x2} refused: not supported");
        return Refuse(header, SmbStatus.UnsupportedCommand);
    }

    private static SmbAnswer Refuse(SmbHeader header, uint status, bool closesConnection = false) =>
        new(SmbResponseEncoder.EncodeError(header, status), closesConnection);

    private SmbAnswer AnswerNegotiate(SmbNegotiateRequest request)
    {
        if (challenge is not null)
        {
            return Refuse(request.Header, SmbStatus.ServerError);
        }

        var dialectIndex = request.Dialects.ToList().IndexOf(Dialect);
        if (dialectIndex < 0)
        {
            // [MS-CIFS] 3.3.5.2: no dialect in common is word count 1 and 0xFFFF.
            return new SmbAnswer(SmbResponseEncoder.Frame(request.Header.ToResponse(SmbStatus.Success), [0xFF, 0xFF], []), ClosesConnection: true);
        }

        challenge = new byte[ChallengeLength];
        challengeSource.Fill(challenge);
        var response = new SmbNegotiateResponse(
            (ushort)dialectIndex,
            UserLevelChallengeResponseSecurity,
            MaxMpxCount: 1,
            MaxNumberVirtualCircuits: 1,
            MaxBufferSize(context.Limits.MaxMessageBytes),
            MaxRawSize,
            SessionKey: 0,
            LargeFilesAndNtSmbsCapabilities,
            context.TimeProvider.GetUtcNow().ToFileTime(),
            ServerTimeZone: 0,
            challenge,
            DomainName);
        return new SmbAnswer(SmbResponseEncoder.EncodeNegotiate(request.Header, response));
    }

    // --max-message, capped at what a NetBIOS frame can carry; 0 (no limit) is that cap.
    private static uint MaxBufferSize(long maxMessageBytes) =>
        maxMessageBytes is > 0 and < LargestMessageLength ? (uint)maxMessageBytes : LargestMessageLength;

    private async ValueTask<SmbAnswer> AnswerSessionSetupAsync(SmbSessionSetupRequest request)
    {
        if (challenge is null || isLoggedIn)
        {
            return Refuse(request.Header, SmbStatus.ServerError);
        }

        var verdict = await CheckLoginAsync(request, challenge);
        if (LoggedOnAs(verdict.Outcome) is not { } loggedOnAs)
        {
            return RefuseLogin(request.Header, verdict);
        }

        isLoggedIn = true;
        return new SmbAnswer(SmbResponseEncoder.EncodeSessionSetup(request.Header, UserId, loggedOnAs, string.Empty, string.Empty, DomainName));
    }

    // Asks the policy and writes the login note it hands back (ADR-0038).
    private async ValueTask<SmbLoginVerdict> CheckLoginAsync(SmbSessionSetupRequest request, byte[] serverChallenge)
    {
        var login = new SmbNtlmV1Login(
            request.AccountName, request.PrimaryDomain, serverChallenge, request.LmResponse, request.NtResponse, connection.TlsSession);
        var verdict = await authenticationPolicy.CheckSmbNtlmV1LoginAsync(login, context.CancellationToken);
        if (verdict.CheckedLogin is { } checkedLogin)
        {
            context.Log.Note(checkedLogin.Note);
        }

        return verdict;
    }

    // The session setup response's action for an accepted login; null for any other outcome.
    private static ushort? LoggedOnAs(SmbLoginOutcome outcome) => outcome switch
    {
        SmbLoginOutcome.Accepted => LoggedOnAsUser,
        SmbLoginOutcome.AcceptedUnchecked => LoggedOnAsGuest,
        _ => null,
    };

    // A refusal with nothing checked means ntlmv1 is not in --auth (ADR-0073, decision 3).
    private SmbAnswer RefuseLogin(SmbHeader header, SmbLoginVerdict verdict)
    {
        if (verdict.CheckedLogin is null)
        {
            context.Log.Note("SMB session setup refused: ntlmv1 is not in --auth");
        }

        return Refuse(header, SmbStatus.BadPassword, closesConnection: true);
    }

    private SmbAnswer AnswerTreeConnect(SmbTreeConnectRequest request)
    {
        if (sharesByTreeId.Count >= MaxTrees)
        {
            return Refuse(request.Header, SmbStatus.ServerError);
        }

        var share = request.Path[(request.Path.LastIndexOf('\\') + 1)..];
        if (!IsShare(share))
        {
            context.Log.Note($"SMB tree connect {share}: no such share");
            return Refuse(request.Header, SmbStatus.InvalidNetworkName);
        }

        var treeId = LowestFreeTreeId();
        sharesByTreeId[treeId] = share;
        context.Log.Note($"SMB tree connect {share}: connected");
        return new SmbAnswer(SmbResponseEncoder.EncodeTreeConnect(request.Header, treeId, 0, DiskShareService, string.Empty));
    }

    // A share is a top-level directory of the content store, mapped as /<share> with every
    // byte but the unreserved ones percent-encoded, so the store's path rules apply unchanged.
    // IPC$, in any case, is never one, whatever the store holds (ADR-0073 decision 2).
    private bool IsShare(string share)
    {
        if (share.Length == 0 || share.Equals(InterProcessCommunicationShare, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var mapping = contentStore.MapRequestPath("/" + Uri.EscapeDataString(share));
        return mapping.IsMapped && mapping.EntryKind == ContentEntryKind.Directory;
    }

    private ushort LowestFreeTreeId()
    {
        ushort treeId = 1;
        while (sharesByTreeId.ContainsKey(treeId))
        {
            treeId++;
        }

        return treeId;
    }

    // Forgets the tree and its files, discarding any upload among them uncommitted.
    private async ValueTask<SmbAnswer> AnswerTreeDisconnectAsync(SmbTreeDisconnectRequest request)
    {
        sharesByTreeId.Remove(request.Header.TreeId);
        foreach (var (fileId, openFile) in openFilesByFileId.Where(entry => entry.Value.TreeId == request.Header.TreeId).ToList())
        {
            openFilesByFileId.Remove(fileId);
            await DiscardUploadAsync(openFile);
        }

        return new SmbAnswer(SmbResponseEncoder.EncodeTreeDisconnect(request.Header));
    }

    // Opens a file of the share for reading, or for writing when the open asks to write
    // (ADR-0073, decisions 2 and 5).
    private async ValueTask<SmbAnswer> AnswerNtCreateAsync(SmbNtCreateRequest request)
    {
        var share = sharesByTreeId[request.Header.TreeId];
        var noteName = $"{share}\\{request.FileName}";
        if (openFilesByFileId.Count >= MaxOpenFiles)
        {
            return RefuseOpen(request.Header, noteName, SmbStatus.NoFileIds, "ERRnofids");
        }

        var mapping = contentStore.MapRequestPath(FileRequestPath(share, request.FileName));
        if (AsksToWrite(request))
        {
            return await OpenForWritingAsync(request, noteName, mapping);
        }

        if ((mapping.IsMapped ? contentStore.GetFileStatus(mapping) : null) is not { } status)
        {
            return RefuseOpen(request.Header, noteName, SmbStatus.BadFile, "ERRbadfile");
        }

        var fileId = LowestFreeFileId();
        openFilesByFileId[fileId] = new SmbOpenFile(request.Header.TreeId, noteName, mapping, status.Length);
        context.Log.Note($"SMB open {noteName} for reading: {status.Length} bytes");
        var lastWriteTime = status.LastModifiedUtc.ToFileTime();
        var response = new SmbNtCreateResponse(
            OplockLevel: 0,
            fileId,
            FileOpened,
            lastWriteTime,
            lastWriteTime,
            lastWriteTime,
            lastWriteTime,
            NormalFileAttributes,
            status.Length,
            status.Length,
            ResourceType: 0,
            NamedPipeStatus: 0,
            IsDirectory: false);
        return new SmbAnswer(SmbResponseEncoder.EncodeNtCreate(request.Header, response));
    }

    private static bool AsksToWrite(SmbNtCreateRequest request) =>
        (request.DesiredAccess & WriteAccessMask) != 0 || request.CreateDisposition != FileOpenDisposition;

    // Opens a random-access upload that creates a missing file and replaces an existing one,
    // as FILE_OVERWRITE_IF asks; a name the path rules refuse is answered as absent, as a read's is.
    private async ValueTask<SmbAnswer> OpenForWritingAsync(SmbNtCreateRequest request, string noteName, ContentPathMapping mapping)
    {
        if (!mapping.IsMapped)
        {
            return RefuseOpen(request.Header, noteName, SmbStatus.BadFile, "ERRbadfile");
        }

        var isReplacingAFile = contentStore.GetFileStatus(mapping) is not null;
        var opening = await contentStore.OpenUploadAsync(mapping, OverwriteIfOpening, context.CancellationToken);
        if (opening.Session is not { } upload)
        {
            return opening.Result == ContentUploadOpeningResult.NoSuchDirectory
                ? RefuseOpen(request.Header, noteName, SmbStatus.BadPath, "ERRbadpath")
                : RefuseOpen(request.Header, noteName, SmbStatus.NoAccess, "ERRnoaccess");
        }

        var fileId = LowestFreeFileId();
        openFilesByFileId[fileId] = new SmbOpenFile(request.Header.TreeId, noteName, mapping, 0) { Upload = upload };
        context.Log.Note($"SMB open {noteName} for writing");
        var now = context.TimeProvider.GetUtcNow().ToFileTime();
        var response = new SmbNtCreateResponse(
            OplockLevel: 0,
            fileId,
            isReplacingAFile ? FileOverwritten : FileCreated,
            now,
            now,
            now,
            now,
            NormalFileAttributes,
            AllocationSize: 0,
            EndOfFile: 0,
            ResourceType: 0,
            NamedPipeStatus: 0,
            IsDirectory: false);
        return new SmbAnswer(SmbResponseEncoder.EncodeNtCreate(request.Header, response));
    }

    private SmbAnswer RefuseOpen(SmbHeader header, string noteName, uint status, string statusName)
    {
        context.Log.Note($"SMB open {noteName} refused: {statusName}");
        return Refuse(header, status);
    }

    // The share, then each backslash-separated part of the name percent-encoded (every byte but
    // the unreserved ones), so the content store's path rules apply unchanged (ADR-0073, decision 2).
    private static string FileRequestPath(string share, string fileName) =>
        "/" + string.Join('/', fileName.Split('\\').Prepend(share).Select(Uri.EscapeDataString));

    private ushort LowestFreeFileId()
    {
        ushort fileId = 1;
        while (openFilesByFileId.ContainsKey(fileId))
        {
            fileId++;
        }

        return fileId;
    }

    // The file open under that FID on the request's tree; null when there is none.
    private SmbOpenFile? FindOpenFile(SmbHeader header, ushort fileId) =>
        openFilesByFileId.GetValueOrDefault(fileId) is { } openFile && openFile.TreeId == header.TreeId ? openFile : null;

    // Answers min(MaxCount, 61440, bytes left) bytes from the offset; at or past the end, none
    // (ADR-0073, decision 5). MaxCountHigh is not read: the negotiate announces no CAP_LARGE_READX.
    private async ValueTask<SmbAnswer> AnswerReadAsync(SmbReadRequest request)
    {
        if (FindOpenFile(request.Header, request.FileId) is not { } openFile)
        {
            return Refuse(request.Header, SmbStatus.BadFileId);
        }

        if (openFile.Upload is not null)
        {
            return Refuse(request.Header, SmbStatus.BadAccess);
        }

        var byteCount = request.Offset < 0 ? 0 : Math.Min(Math.Min((long)request.MaxCount, MaxReadBytes), openFile.Length - request.Offset);
        if (byteCount <= 0)
        {
            return new SmbAnswer(SmbResponseEncoder.EncodeRead(request.Header, 0, []));
        }

        using var data = new MemoryStream();
        try
        {
            var range = ContentByteRange.Select(openFile.Length, request.Offset, request.Offset + byteCount - 1);
            await contentStore.CopyFileBytesAsync(openFile.Mapping, range, data, context.CancellationToken);
        }
        catch (IOException exception)
        {
            context.Log.Note($"SMB read {openFile.NoteName} failed: {exception.Message}");
            return Refuse(request.Header, SmbStatus.GeneralFailure);
        }

        openFile.BytesRead += data.Length;
        return new SmbAnswer(SmbResponseEncoder.EncodeRead(request.Header, 0, data.GetBuffer().AsSpan(0, (int)data.Length)));
    }

    private async ValueTask<SmbAnswer> AnswerCloseAsync(SmbCloseRequest request)
    {
        if (FindOpenFile(request.Header, request.FileId) is not { } openFile)
        {
            return Refuse(request.Header, SmbStatus.BadFileId);
        }

        if (openFile.Upload is not { } upload)
        {
            openFilesByFileId.Remove(request.FileId);
            context.Log.Note($"SMB close {openFile.NoteName}: {openFile.BytesRead} bytes read");
            return new SmbAnswer(SmbResponseEncoder.EncodeClose(request.Header));
        }

        // Every way the commit returns ends the upload; one that throws leaves it open, so the
        // file is forgotten only afterwards and the session's disposal discards it otherwise.
        var answer = await CommitUploadAsync(request.Header, openFile, upload);
        openFilesByFileId.Remove(request.FileId);
        return answer;
    }

    // The close commits the upload (ADR-0073, decision 5). One a write already refused or failed
    // was discarded then, so its close is answered with success, as curl's close after an error
    // is; a target that can no longer take a file is ERRnoaccess, a store failure ERRgeneral.
    private async ValueTask<SmbAnswer> CommitUploadAsync(SmbHeader header, SmbOpenFile openFile, ContentUploadSession upload)
    {
        if (openFile.HasUploadFailed)
        {
            context.Log.Note($"SMB close {openFile.NoteName}: upload discarded");
            return new SmbAnswer(SmbResponseEncoder.EncodeClose(header));
        }

        var length = upload.Length;
        ContentUploadResult result;
        try
        {
            result = await upload.CommitAsync(context.CancellationToken);
        }
        catch (IOException exception)
        {
            context.Log.Note($"SMB close {openFile.NoteName} failed: {exception.Message}");
            return Refuse(header, SmbStatus.GeneralFailure);
        }

        return result switch
        {
            ContentUploadResult.Written => ClosedUpload(header, $"SMB close {openFile.NoteName}: {length} bytes written"),
            ContentUploadResult.TooLarge => ClosedUpload(header, $"SMB close {openFile.NoteName}: upload discarded"),
            _ => ClosedUpload(header, $"SMB close {openFile.NoteName} refused: ERRnoaccess", SmbStatus.NoAccess),
        };
    }

    private SmbAnswer ClosedUpload(SmbHeader header, string note, uint status = SmbStatus.Success)
    {
        context.Log.Note(note);
        return new SmbAnswer(SmbResponseEncoder.EncodeError(header, status));
    }

    // Writes the data at its offset and answers exactly the count received, which is where curl
    // puts its next write (ADR-0073, decision 5); a write on a file opened for reading is
    // ERRbadaccess, one past --max-filesize ERRdiskfull with the partial file deleted.
    private ValueTask<SmbAnswer> AnswerWriteAsync(SmbWriteRequest request)
    {
        var openFile = FindOpenFile(request.Header, request.FileId);
        if (openFile?.Upload is not { } upload)
        {
            return ValueTask.FromResult(Refuse(request.Header, openFile is null ? SmbStatus.BadFileId : SmbStatus.BadAccess));
        }

        return WriteRefusal(request, openFile) is { } status
            ? ValueTask.FromResult(Refuse(request.Header, status))
            : WriteUploadAsync(request, openFile, upload);
    }

    // ERRSRV/ERRerror for an offset no file can have, ERRHRD/ERRgeneral once a write has failed;
    // null when the write may go ahead.
    private static uint? WriteRefusal(SmbWriteRequest request, SmbOpenFile openFile)
    {
        if (request.Offset < 0 || request.Offset > long.MaxValue - request.Data.Length)
        {
            return SmbStatus.ServerError;
        }

        return openFile.HasUploadFailed ? SmbStatus.GeneralFailure : null;
    }

    private async ValueTask<SmbAnswer> WriteUploadAsync(SmbWriteRequest request, SmbOpenFile openFile, ContentUploadSession upload)
    {
        ContentUploadResult result;
        try
        {
            result = await upload.WriteAtAsync(request.Offset, request.Data, context.CancellationToken);
        }
        catch (IOException exception)
        {
            openFile.HasUploadFailed = true;
            context.Log.Note($"SMB write {openFile.NoteName} failed: {exception.Message}");
            return Refuse(request.Header, SmbStatus.GeneralFailure);
        }

        if (result != ContentUploadResult.Written)
        {
            context.Log.Note($"SMB write {openFile.NoteName} refused: past --max-filesize {contentStore.ExposureOptions.MaxUploadBytes}");
            return Refuse(request.Header, SmbStatus.DiskFull);
        }

        return new SmbAnswer(SmbResponseEncoder.EncodeWrite(request.Header, (ushort)request.Data.Length, 0));
    }
}
