namespace Surl.Extended.Protocol.Redis;

/// <summary>
/// One connection's state (ADR-0106 decisions 2 to 4): its client id, protocol version, login,
/// selected database and name.
/// </summary>
/// <param name="clientId">The connection's client id.</param>
/// <param name="isAnonymousAllowed">Whether the policy lets an anonymous caller in (<c>--allow-anonymous</c>).</param>
internal sealed class RedisSession(long clientId, bool isAnonymousAllowed)
{
    /// <summary>How many databases there are, numbered from 0 (Redis's default <c>databases 16</c>).</summary>
    public const int DatabaseCount = 16;

    /// <summary>The connection's client id.</summary>
    public long ClientId { get; } = clientId;

    /// <summary>Whether the policy lets an anonymous caller in.</summary>
    public bool IsAnonymousAllowed { get; } = isAnonymousAllowed;

    /// <summary>The RESP version replies are written in: 2 until <c>HELLO 3</c>.</summary>
    public int ProtocolVersion { get; set; } = 2;

    /// <summary>The account logged in as, or <see langword="null"/> before a login.</summary>
    public string? UserName { get; private set; }

    /// <summary>Whether every command may run: a login was accepted, or anonymous callers are let in.</summary>
    public bool MayRunEveryCommand => IsAnonymousAllowed || UserName is not null;

    /// <summary>The selected database, 0 to 15.</summary>
    public int DatabaseIndex { get; set; }

    /// <summary>The name <c>CLIENT SETNAME</c> or <c>HELLO … SETNAME</c> gave, or <see langword="null"/>.</summary>
    public byte[]? ClientName { get; set; }

    /// <summary>Whether <c>QUIT</c> was answered, so the connection closes after the reply.</summary>
    public bool IsQuitting { get; set; }

    /// <summary>A missing value in the connection's version: <c>$-1</c> in RESP2, <c>_</c> in RESP3.</summary>
    public RespValue NullReply => ProtocolVersion == 3 ? RespNull.Instance : RespNullBulkString.Instance;

    /// <summary>Records an accepted login as <paramref name="userName"/>.</summary>
    /// <param name="userName">The account.</param>
    public void LogIn(string userName) => UserName = userName;

    /// <summary>
    /// What <c>RESET</c> does (ADR-0106 decision 4): logs out, selects database 0, clears the name
    /// and returns to RESP2.
    /// </summary>
    public void Reset()
    {
        UserName = null;
        DatabaseIndex = 0;
        ClientName = null;
        ProtocolVersion = 2;
    }
}
