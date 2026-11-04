using Surl.Content;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Smb;

/// <summary>
/// The SMB version 1 server upstream curl's <c>smb://</c> and <c>smbs://</c> transfers talk to
/// (FR-050): answers <c>SMB_COM_NEGOTIATE</c>, the NTLMv1 <c>SMB_COM_SESSION_SETUP_ANDX</c>,
/// <c>SMB_COM_TREE_CONNECT_ANDX</c>, <c>SMB_COM_TREE_DISCONNECT</c>, and the
/// <c>SMB_COM_NT_CREATE_ANDX</c>, <c>SMB_COM_READ_ANDX</c>, <c>SMB_COM_WRITE_ANDX</c> and
/// <c>SMB_COM_CLOSE</c> of a download from or an upload to the content store, as ADR-0073 decides.
/// </summary>
/// <remarks>
/// <para>
/// <b>Negotiate</b> (decision 1). <c>NT LM 0.12</c> without extended security: user-level
/// challenge/response security, one request and one circuit, <c>--max-message</c> (at most
/// 131071) as the buffer size, <c>CAP_LARGE_FILES</c> and <c>CAP_NT_SMBS</c>, the injected
/// clock's time, a fresh 8-byte challenge from <see cref="ISmbChallengeSource"/> and the domain
/// <c>SURL</c>. A negotiate without <c>NT LM 0.12</c> is answered word count 1 and
/// <c>0xFFFF</c> and the connection closed; a second negotiate is <c>ERRSRV/ERRerror</c>.
/// </para>
/// <para>
/// <b>Session setup</b> (decision 3). The user, domain, challenge, LM and NT responses and the
/// TLS session go to <see cref="ISmbAuthenticationPolicy.CheckSmbNtlmV1LoginAsync"/>, which
/// alone decides; its <see cref="CheckedLogin.Note"/> is written. <c>Accepted</c> is answered
/// with action 0 and UID 1, <c>AcceptedUnchecked</c> with action 1 (guest); anything else is
/// <c>ERRSRV/ERRbadpw</c> and the connection is closed, noted
/// <c>SMB session setup refused: ntlmv1 is not in --auth</c> when nothing was checked.
/// </para>
/// <para>
/// <b>Trees</b> (decision 2). A share is a top-level directory of the content store, named by
/// the last backslash-separated part of the tree connect's path; anything else, and <c>IPC$</c>
/// in any case whatever the store holds, is <c>ERRSRV/ERRinvnetname</c>. Up to 16 trees, TIDs from 1. Every request but negotiate and
/// session setup needs the login's UID (<c>ERRSRV/ERRbaduid</c>) and every request but tree
/// connect a connected TID (<c>ERRSRV/ERRinvtid</c>). A command curl never sends is
/// <c>ERRSRV/ERRsmbcmd</c>, a malformed message <c>ERRSRV/ERRerror</c>.
/// </para>
/// <para>
/// <b>Reads</b> (decisions 2 and 5). An NT create with <c>FILE_OPEN</c> and no write access opens
/// the share's file named by its backslash-separated components, each percent-encoded and
/// mapped through <see cref="ContentStore.MapRequestPath"/>: FIDs from 1, at most 16 open
/// (<c>ERRDOS/ERRnofids</c>), answered <c>FILE_OPENED</c> with the file's last write time as all
/// four times, <c>FILE_ATTRIBUTE_NORMAL</c> and its length. Anything that is not a served file -
/// absent, hidden, refused by the path rules, a directory, the share itself - is
/// <c>ERRDOS/ERRbadfile</c>. Each read answers at most its
/// <c>MaxCount</c>, 61440 and the bytes left from its offset, none at or past the end;
/// a content store failure is <c>ERRHRD/ERRgeneral</c>. A close forgets the FID, a tree
/// disconnect its tree's FIDs; a read, write or close of a FID not open on that tree is
/// <c>ERRDOS/ERRbadfid</c>, a write on a file opened for reading or a read on one opened for
/// writing <c>ERRDOS/ERRbadaccess</c>.
/// </para>
/// <para>
/// <b>Uploads</b> (decision 5). An NT create that asks for write access or a disposition other
/// than <c>FILE_OPEN</c> opens a random-access upload through
/// <see cref="ContentStore.OpenUploadAsync"/> that creates a missing file and replaces an
/// existing one, answered <c>FILE_CREATED</c> or <c>FILE_OVERWRITTEN</c> with the clock's time
/// and a length of 0. Uploads off (no <c>--allow-uploads</c>), a hidden or <c>.surl</c>
/// location and a directory at the name are <c>ERRDOS/ERRnoaccess</c>; a missing directory
/// <c>ERRDOS/ERRbadpath</c>; a name the path rules refuse <c>ERRDOS/ERRbadfile</c>. Each write
/// lands at its offset and is answered with exactly the count it carried. A write past
/// <c>--max-filesize</c> is <c>ERRHRD/ERRdiskfull</c> and the partial file is deleted, a
/// content store failure <c>ERRHRD/ERRgeneral</c>; the close curl then sends is answered with
/// success. The close commits the upload over the target; a tree disconnect, or the end of the
/// connection, discards one still open.
/// </para>
/// <para>
/// <b>Keep answering</b> (decision 5). Curl does not notice the server close while it waits,
/// so every request read is answered and the connection stays open, except after a refused
/// session setup or a negotiate without the dialect. A NetBIOS keep-alive is ignored; any
/// other frame type, or a message that does not open with <c>0xFF 'SMB'</c>, closes it.
/// </para>
/// <para>
/// <b>Limits</b> (decision 7). A message past <see cref="ExchangeLimits.MaxMessageBytes"/> is
/// answered <c>ERRSRV/ERRerror</c> once its header is in, the rest is discarded, and the
/// session goes on. No negotiate within <see cref="ExchangeLimits.HeadTimeout"/> closes the
/// connection with nothing sent. When the engine cancels the exchange for its idle timeout or
/// maximum duration, a request being answered is answered <c>ERRSRV/ERRerror</c> within
/// <see cref="LimitReplyWriteDeadline"/> and the connection closed; with none being answered it
/// is closed with nothing sent. At shutdown the cancellation propagates, with no farewell
/// (ADR-0059).
/// </para>
/// </remarks>
public sealed class SmbProtocolServer : IConnectionProtocolServer
{
    /// <summary>
    /// How long the error answering a request cut off by a limit may take to write (ADR-0059).
    /// </summary>
    public static readonly TimeSpan LimitReplyWriteDeadline = TimeSpan.FromSeconds(1);

    private readonly ContentStore contentStore;
    private readonly ISmbAuthenticationPolicy authenticationPolicy;
    private readonly ISmbChallengeSource challengeSource;

    /// <summary>
    /// Creates an SMB server over <paramref name="contentStore"/> whose session setups
    /// <paramref name="authenticationPolicy"/> judges.
    /// </summary>
    /// <param name="contentStore">The content store whose top-level directories are the shares.</param>
    /// <param name="authenticationPolicy">Who may log in.</param>
    /// <param name="challengeSource">
    /// Where each connection's challenge comes from; <see langword="null"/> for
    /// <see cref="SmbSystemChallengeSource"/>.
    /// </param>
    public SmbProtocolServer(ContentStore contentStore, ISmbAuthenticationPolicy authenticationPolicy, ISmbChallengeSource? challengeSource = null)
    {
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(authenticationPolicy);

        this.contentStore = contentStore;
        this.authenticationPolicy = authenticationPolicy;
        this.challengeSource = challengeSource ?? new SmbSystemChallengeSource();
    }

    /// <summary>
    /// The schemes answered: <c>smb</c>, and <c>smbs</c>, whose TLS handshake the engine
    /// completes before <see cref="ServeAsync"/> (ADR-0073, decision 6).
    /// </summary>
    public IReadOnlyList<string> Schemes { get; } = Array.AsReadOnly(["smb", "smbs"]);

    /// <summary>
    /// Answers every SMB message on <paramref name="connection"/> until the client closes it,
    /// the server closes it, or the engine cancels the exchange.
    /// </summary>
    /// <param name="connection">The accepted connection.</param>
    /// <param name="context">What the server is told about this exchange.</param>
    /// <returns>A task that completes when the exchange is over.</returns>
    public Task ServeAsync(IConnection connection, ExchangeContext context)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(context);

        return ServeSessionAsync(connection, context);
    }

    // The session is disposed however the exchange ends, so an upload left open is discarded
    // (ADR-0073, decision 5); then the exchange's own outcome, cancellation included, goes on.
    // Awaiting without throwing first, rather than in a finally, leaves the compiler no rethrow
    // branch that no exception can take.
    private async Task ServeSessionAsync(IConnection connection, ExchangeContext context)
    {
        var session = new SmbSession(connection, context, contentStore, authenticationPolicy, challengeSource);
        var serving = new SmbExchange(connection, context, session).ServeAsync();
        await serving.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await session.DisposeAsync();
        await serving;
    }
}
