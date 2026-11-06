using System.Buffers.Text;
using System.Security.Cryptography;
using Surl.McpServing;

namespace Surl.Extended.Protocol.McpServer;

/// <summary>
/// The server's live Streamable HTTP sessions (ADR-0111 decision 4.5). Each ID is 32 bytes from the injected
/// random source, base64url without padding. A session belongs to the login that opened it: another login
/// naming it finds nothing. It ends after <see cref="IdleLifetime" /> without a request, on the injected clock,
/// or when it is ended; at most <see cref="MaximumSessions" /> live at once. Safe to call from several threads.
/// </summary>
/// <param name="idSource">Where session IDs come from: <see cref="RandomNumberGenerator" /> in production.</param>
/// <param name="timeProvider">The clock expiry is measured on.</param>
internal sealed class McpHttpSessionTable(RandomNumberGenerator idSource, TimeProvider timeProvider)
{
    /// <summary>The most sessions the server holds at once.</summary>
    public const int MaximumSessions = 1000;

    /// <summary>The bytes of randomness in one session ID.</summary>
    public const int IdBytes = 32;

    /// <summary>How long a session lives without a request.</summary>
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(30);

    private readonly Dictionary<string, McpHttpSession> _sessions = new(StringComparer.Ordinal);

    private readonly Lock _lock = new();

    /// <summary>Adds an initialized session.</summary>
    /// <param name="login">The account that opened it; <see langword="null" /> for the anonymous caller.</param>
    /// <param name="session">The core session, already initialized.</param>
    /// <returns>Its <c>Mcp-Session-Id</c>, or <see langword="null" /> when the table is full and nothing was added.</returns>
    public string? Add(string? login, McpServerSession session)
    {
        lock (_lock)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            RemoveExpired(now);
            if (_sessions.Count >= MaximumSessions)
            {
                return null;
            }

            byte[] random = new byte[IdBytes];
            idSource.GetBytes(random);
            string id = Base64Url.EncodeToString(random);
            _sessions[id] = new McpHttpSession(id, login, session, now);
            return id;
        }
    }

    /// <summary>Finds a live session the login opened, and marks it used.</summary>
    /// <param name="id">The <c>Mcp-Session-Id</c> the request named.</param>
    /// <param name="login">The request's login; <see langword="null" /> for the anonymous caller.</param>
    /// <returns>The session, or <see langword="null" /> when no live session of that login has the ID.</returns>
    public McpHttpSession? Find(string id, string? login)
    {
        lock (_lock)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            RemoveExpired(now);
            if (!_sessions.TryGetValue(id, out McpHttpSession? session) || !string.Equals(session.Login, login, StringComparison.Ordinal))
            {
                return null;
            }

            session.LastUsed = now;
            return session;
        }
    }

    /// <summary>Ends a session: it is removed, and its running requests are cancelled.</summary>
    /// <param name="session">A session <see cref="Find" /> returned.</param>
    public void End(McpHttpSession session)
    {
        lock (_lock)
        {
            _sessions.Remove(session.Id);
        }

        session.End();
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (McpHttpSession expired in _sessions.Values.Where(session => now - session.LastUsed >= IdleLifetime).ToArray())
        {
            _sessions.Remove(expired.Id);
            expired.End();
        }
    }
}
