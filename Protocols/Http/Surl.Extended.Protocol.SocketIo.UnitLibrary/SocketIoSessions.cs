using System.Security.Cryptography;

namespace Surl.Extended.Protocol.SocketIo;

/// <summary>
/// The Engine.IO sessions of one listener, shared by every connection it serves (ADR-0120,
/// decisions 1, 5 and 7): at most <see cref="MaxSessions"/> at once, each found by its
/// <c>sid</c>, and the set a <c>broadcast</c> event reaches.
/// </summary>
/// <remarks>
/// A session's <c>sid</c> is reserved when its upgrade or polling handshake is decided, so the limit
/// counts it from the <c>101</c> or the open packet on, and its <see cref="SocketIoSession"/> is
/// attached once it is created.
/// Each <c>sid</c> is <see cref="SocketIoRandomId"/>'s 15 bytes from
/// <see cref="RandomNumberGenerator"/>: it names a session, so it is never drawn from a seeded source.
/// </remarks>
public sealed class SocketIoSessions
{
    /// <summary>
    /// The most sessions a listener holds (ADR-0120, decision 7; ADR-0006's connection ceiling).
    /// </summary>
    public const int MaxSessions = 1024;

    private readonly Lock gate = new();
    private readonly Dictionary<string, SocketIoSession?> sessions = new(StringComparer.Ordinal);

    /// <summary>
    /// How many sessions are reserved or live.
    /// </summary>
    public int Count
    {
        get
        {
            lock (gate)
            {
                return sessions.Count;
            }
        }
    }

    /// <summary>
    /// Whether a live or reserved session has <paramref name="sessionId"/>, and its
    /// <see cref="SocketIoSession"/> once one is attached.
    /// </summary>
    internal bool TryFind(string sessionId, out SocketIoSession? session)
    {
        lock (gate)
        {
            return sessions.TryGetValue(sessionId, out session);
        }
    }

    /// <summary>
    /// Reserves a fresh <c>sid</c>, or returns <see langword="null"/> when the listener holds
    /// <see cref="MaxSessions"/>.
    /// </summary>
    internal string? TryReserve()
    {
        lock (gate)
        {
            if (sessions.Count >= MaxSessions)
            {
                return null;
            }

            using var random = RandomNumberGenerator.Create();
            string sessionId;
            do
            {
                sessionId = SocketIoRandomId.Create(random);
            }
            while (!sessions.TryAdd(sessionId, null));

            return sessionId;
        }
    }

    /// <summary>
    /// Attaches the session served under a reserved <c>sid</c>, so broadcasts reach it.
    /// </summary>
    internal void Attach(string sessionId, SocketIoSession session)
    {
        lock (gate)
        {
            sessions[sessionId] = session;
        }
    }

    /// <summary>
    /// Forgets a session, so its <c>sid</c> is unknown and its place free.
    /// </summary>
    internal void Remove(string sessionId)
    {
        lock (gate)
        {
            sessions.Remove(sessionId);
        }
    }

    /// <summary>
    /// The attached sessions, as they stand.
    /// </summary>
    internal SocketIoSession[] Attached()
    {
        lock (gate)
        {
            return [.. sessions.Values.OfType<SocketIoSession>()];
        }
    }
}
