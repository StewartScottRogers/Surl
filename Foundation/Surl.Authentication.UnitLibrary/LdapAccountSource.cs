using System.Security.Authentication;
using System.Text;

namespace Surl.Authentication;

/// <summary>
/// Checks a password login against an external LDAP directory by searching for the user's entry and
/// binding as it with the password the peer sent (ADR-0149, decisions 4 to 6 and 9 to 11). One
/// connection per login, no cache. Every failure - no such entry, a wrong password, a timeout, a
/// refused connection - is the same refusal to the peer; only <c>note</c> tells the operator why.
/// </summary>
public sealed class LdapAccountSource
{
    /// <summary>The directory checks that may run at once (ADR-0149, decision 10).</summary>
    public const int MaxConcurrentChecks = 16;

    /// <summary>The longest user name sent to a directory, in UTF-8 bytes.</summary>
    public const int MaxUserNameBytes = 256;

    private const int InvalidCredentials = 49;
    private const int Referral = 10;
    private const int SizeLimitExceeded = 4;

    private readonly LdapAccountSourceSettings settings;
    private readonly ILdapDirectoryConnector connector;
    private readonly TimeProvider timeProvider;
    private readonly Action<string>? note;
    private readonly SemaphoreSlim slots = new(MaxConcurrentChecks, MaxConcurrentChecks);

    /// <summary>
    /// Checks logins against the directory <paramref name="settings"/> names, through
    /// <paramref name="connector"/>.
    /// </summary>
    /// <param name="settings">The directory and how to search it.</param>
    /// <param name="connector">How a connection is made.</param>
    /// <param name="timeProvider">The clock the timeout runs on.</param>
    /// <param name="note">Receives the verbose-log line of each refusal the directory judged; <see langword="null"/> logs none.</param>
    public LdapAccountSource(
        LdapAccountSourceSettings settings, ILdapDirectoryConnector connector, TimeProvider timeProvider, Action<string>? note = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.settings = settings;
        this.connector = connector;
        this.timeProvider = timeProvider;
        this.note = note;
    }

    /// <summary>
    /// Whether a directory account may log in on <paramref name="scheme"/> (<c>protocols=</c>, ADR-0149 decision 8).
    /// </summary>
    /// <param name="scheme">The scheme, compared without regard to case.</param>
    /// <returns><see langword="true"/> when every scheme is served or the scheme is named.</returns>
    public bool Serves(string scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        return settings.Protocols is null || settings.Protocols.Contains(scheme, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks one login. A name the directory is not asked about - a scheme it does not serve, an
    /// empty or over-long name, a name holding U+0000, an empty password - is refused without
    /// contacting it (decision 6; RFC 4513 section 5.1.2 forbids sending an empty password).
    /// </summary>
    /// <param name="scheme">The scheme the login was made over.</param>
    /// <param name="userName">The user name as the peer sent it.</param>
    /// <param name="password">The password bytes as the peer sent them.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The directory's verdict.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async ValueTask<LdapLoginResult> CheckAsync(
        string scheme, string userName, ReadOnlyMemory<byte> password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(userName);
        if (!Serves(scheme) || password.IsEmpty || userName.Length == 0
            || userName.Contains('\0', StringComparison.Ordinal) || Encoding.UTF8.GetByteCount(userName) > MaxUserNameBytes)
        {
            return new LdapLoginResult(false, null);
        }

        var result = await CheckWithinTimeoutAsync(userName, password, cancellationToken).ConfigureAwait(false);
        if (!result.IsAccepted)
        {
            note?.Invoke($"Login refused: password {userName} (directory: {result.Reason})");
        }

        return result;
    }

    private async ValueTask<LdapLoginResult> CheckWithinTimeoutAsync(
        string userName, ReadOnlyMemory<byte> password, CancellationToken cancellationToken)
    {
        var timeoutLength = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        var startedAt = timeProvider.GetTimestamp();
        using var timeout = new CancellationTokenSource(timeoutLength, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await slots.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                // A slot freed by a check that timed out can reach this one before its own timer has fired;
                // the timeout still covers the wait, so a check already past it never connects.
                if (timeProvider.GetElapsedTime(startedAt) >= timeoutLength)
                {
                    timeout.Cancel();
                    linked.Token.ThrowIfCancellationRequested();
                }


                return await ExchangeAsync(userName, password, linked.Token).ConfigureAwait(false);
            }
            finally
            {
                slots.Release();
            }
        }
        catch (LdapCheckRefusedException refusal)
        {
            return new LdapLoginResult(false, refusal.Reason);
        }
        catch (AuthenticationException)
        {
            return new LdapLoginResult(false, "TLS failed");
        }
        catch (IOException)
        {
            return new LdapLoginResult(false, "malformed answer");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LdapLoginResult(false, $"timed out after {settings.TimeoutSeconds} s");
        }
    }

    private async ValueTask<LdapLoginResult> ExchangeAsync(
        string userName, ReadOnlyMemory<byte> password, CancellationToken cancellationToken)
    {
        var url = settings.Url;
        await using var connection = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var stream = connection.Stream;
        var messageId = 0;
        if (!url.UsesTlsFromTheStart && !settings.IsPlaintext)
        {
            var startTls = await RoundTripAsync(connection, LdapMessageCodec.StartTlsRequest(++messageId), messageId, LdapResponseKind.Extended, cancellationToken)
                .ConfigureAwait(false);
            RequireSuccess(startTls, "StartTLS refused, result ");
            await connection.StartTlsAsync(cancellationToken).ConfigureAwait(false);
        }

        if (settings.BindDn is not null)
        {
            var serviceBind = await RoundTripAsync(
                connection, LdapMessageCodec.BindRequest(++messageId, settings.BindDn, settings.BindPassword.ToArray()), messageId, LdapResponseKind.Bind, cancellationToken)
                .ConfigureAwait(false);
            RequireSuccess(serviceBind, "service bind refused, result ");
        }

        var entryName = await SearchAsync(connection, ++messageId, userName, cancellationToken).ConfigureAwait(false);
        var userBind = await RoundTripAsync(
            connection, LdapMessageCodec.BindRequest(++messageId, entryName, password.ToArray()), messageId, LdapResponseKind.Bind, cancellationToken)
            .ConfigureAwait(false);
        var verdict = BindVerdict(userBind);
        await SendUnbindAsync(stream, ++messageId, cancellationToken).ConfigureAwait(false);

        return verdict;
    }

    private static LdapLoginResult BindVerdict(LdapResponse userBind) => userBind.ResultCode switch
    {
        0 => new LdapLoginResult(true, null),
        InvalidCredentials => new LdapLoginResult(false, "wrong password"),
        var code => new LdapLoginResult(false, $"bind refused, result {code}"),
    };

    private static void RequireSuccess(LdapResponse response, string reasonPrefix)
    {
        if (response.ResultCode != 0)
        {
            throw LdapCheckRefusedException.Because(reasonPrefix + response.ResultCode);
        }
    }

    private async ValueTask<ILdapDirectoryConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var url = settings.Url;
        try
        {
            return await connector.ConnectAsync(new LdapDirectoryEndpoint(url.Host, url.Port, url.UsesTlsFromTheStart), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or System.Net.Sockets.SocketException)
        {
            throw LdapCheckRefusedException.Because("could not connect");
        }
    }

    private static async ValueTask<LdapResponse> RoundTripAsync(
        ILdapDirectoryConnection connection, byte[] request, int messageId, LdapResponseKind expected, CancellationToken cancellationToken)
    {
        await connection.Stream.WriteAsync(request, cancellationToken).ConfigureAwait(false);
        await connection.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        return await ReadExpectedAsync(connection.Stream, messageId, expected, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<LdapResponse> ReadExpectedAsync(
        Stream stream, int messageId, LdapResponseKind expected, CancellationToken cancellationToken)
    {
        var response = await LdapMessageCodec.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        if (response.MessageId != messageId || response.Kind != expected)
        {
            throw LdapCheckRefusedException.MalformedAnswer();
        }

        return response;
    }

    // Returns the DN of the one entry the search found; every other outcome ends the check as a refusal.
    private async ValueTask<string> SearchAsync(
        ILdapDirectoryConnection connection, int messageId, string userName, CancellationToken cancellationToken)
    {
        var request = LdapMessageCodec.SearchRequest(messageId, settings.Url, userName, settings.TimeoutSeconds);
        await connection.Stream.WriteAsync(request, cancellationToken).ConfigureAwait(false);
        await connection.Stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<string>();
        var hasReferral = false;
        while (true)
        {
            var response = await LdapMessageCodec.ReadAsync(connection.Stream, cancellationToken).ConfigureAwait(false);
            if (response.MessageId != messageId)
            {
                throw LdapCheckRefusedException.MalformedAnswer();
            }

            switch (response.Kind)
            {
                case LdapResponseKind.SearchEntry:
                    entries.Add(response.EntryName!);
                    if (entries.Count > 1)
                    {
                        throw LdapCheckRefusedException.Because("ambiguous entry");
                    }

                    break;
                case LdapResponseKind.SearchReference:
                    hasReferral = true;
                    break;
                case LdapResponseKind.SearchDone:
                    return EntryFrom(entries, hasReferral, response.ResultCode);
                default:
                    throw LdapCheckRefusedException.MalformedAnswer();
            }
        }
    }

    private static string EntryFrom(List<string> entries, bool hasReferral, int resultCode)
    {
        if (resultCode == SizeLimitExceeded)
        {
            throw LdapCheckRefusedException.Because("ambiguous entry");
        }

        if (resultCode == Referral || (resultCode == 0 && entries.Count == 0 && hasReferral))
        {
            throw LdapCheckRefusedException.Because("referral not followed");
        }

        if (resultCode != 0)
        {
            throw LdapCheckRefusedException.Because($"search refused, result {resultCode}");
        }

        return entries.Count == 1 ? entries[0] : throw LdapCheckRefusedException.Because("no such entry");
    }

    private static async ValueTask SendUnbindAsync(Stream stream, int messageId, CancellationToken cancellationToken)
    {
        try
        {
            await stream.WriteAsync(LdapMessageCodec.UnbindRequest(messageId), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The verdict is already known; a connection that ends before the unbind changes nothing.
        }
    }
}
