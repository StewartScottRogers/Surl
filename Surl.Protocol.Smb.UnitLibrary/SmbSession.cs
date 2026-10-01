using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smb;

/// <summary>
/// One connection's SMB version 1 session: the negotiate, the NTLMv1 session setup checked
/// through <see cref="ISmbAuthenticationPolicy"/>, and the trees connected to shares, answering
/// each decoded request as ADR-0073 decisions 1 to 4 say.
/// </summary>
/// <remarks>
/// Files are not served yet: an <c>SMB_COM_NT_CREATE_ANDX</c> on a connected tree is answered
/// <c>ERRDOS/ERRbadfile</c>, so no FID is ever open and a read, write or close is answered
/// <c>ERRDOS/ERRbadfid</c>. Not safe for concurrent calls.
/// </remarks>
internal sealed class SmbSession
{
    /// <summary>The UID of the connection's one session (ADR-0073, decision 2).</summary>
    public const ushort UserId = 1;

    /// <summary>The most trees one session may have connected at once (ADR-0073, decision 2).</summary>
    public const int MaxTrees = 16;

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

    private readonly IConnection connection;
    private readonly ExchangeContext context;
    private readonly ContentStore contentStore;
    private readonly ISmbAuthenticationPolicy authenticationPolicy;
    private readonly ISmbChallengeSource challengeSource;
    private readonly Dictionary<ushort, string> sharesByTreeId = [];
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
        _ => AnswerLoggedIn(request),
    };

    private SmbAnswer AnswerLoggedIn(SmbRequest request) => request switch
    {
        SmbTreeConnectRequest treeConnect => AnswerTreeConnect(treeConnect),
        _ when !sharesByTreeId.ContainsKey(request.Header.TreeId) => Refuse(request.Header, SmbStatus.InvalidTreeId),
        SmbTreeDisconnectRequest treeDisconnect => AnswerTreeDisconnect(treeDisconnect),
        SmbNtCreateRequest ntCreate => AnswerNtCreate(ntCreate),
        _ => Refuse(request.Header, SmbStatus.BadFileId),
    };

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
    private bool IsShare(string share)
    {
        if (share.Length == 0)
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

    private SmbAnswer AnswerTreeDisconnect(SmbTreeDisconnectRequest request)
    {
        sharesByTreeId.Remove(request.Header.TreeId);
        return new SmbAnswer(SmbResponseEncoder.EncodeTreeDisconnect(request.Header));
    }

    private SmbAnswer AnswerNtCreate(SmbNtCreateRequest request)
    {
        context.Log.Note($"SMB open {sharesByTreeId[request.Header.TreeId]}\\{request.FileName} refused: ERRbadfile");
        return Refuse(request.Header, SmbStatus.BadFile);
    }
}
