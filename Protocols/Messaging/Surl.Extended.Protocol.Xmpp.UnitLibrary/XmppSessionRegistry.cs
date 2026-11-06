namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The streams one <see cref="XmppServer"/> has bound, which stanzas are routed between (ADR-0114
/// decision 5). One per server instance, never a static, so servers side by side never see each
/// other's users. Safe to call from every connection's task.
/// </summary>
internal sealed class XmppSessionRegistry
{
    private readonly Lock gate = new();
    private readonly List<XmppBoundSession> sessions = [];

    /// <summary>Adds a stream once its resource is bound.</summary>
    /// <param name="session">The bound stream.</param>
    public void Add(XmppBoundSession session)
    {
        lock (gate)
        {
            sessions.Add(session);
        }
    }

    /// <summary>Removes a stream when its connection ends.</summary>
    /// <param name="session">The stream.</param>
    public void Remove(XmppBoundSession session)
    {
        lock (gate)
        {
            sessions.Remove(session);
        }
    }

    /// <summary>
    /// The stream bound to a full JID.
    /// </summary>
    /// <param name="jid">The JID a stanza is addressed to.</param>
    /// <returns>The stream, or <see langword="null"/> when the JID is bare or its resource is not bound.</returns>
    public XmppBoundSession? Bound(XmppJid jid)
    {
        lock (gate)
        {
            return sessions.FirstOrDefault(session => jid.HasBare(session.BareJid) && session.Resource == jid.Resource);
        }
    }

    /// <summary>Whether any stream is bound for a bare JID.</summary>
    /// <param name="jid">The JID a stanza is addressed to; its resource is ignored.</param>
    /// <returns><see langword="true"/> when at least one stream of the account is bound.</returns>
    public bool HasBound(XmppJid jid)
    {
        lock (gate)
        {
            return sessions.Any(session => jid.HasBare(session.BareJid));
        }
    }

    /// <summary>
    /// The streams a message to a bare JID goes to (RFC 6121 8.5.2.1.1): every available one of
    /// the account with a non-negative priority.
    /// </summary>
    /// <param name="jid">The JID a message is addressed to; its resource is ignored.</param>
    /// <returns>The streams, in the order they were bound.</returns>
    public IReadOnlyList<XmppBoundSession> AvailableWithNonNegativePriority(XmppJid jid)
    {
        lock (gate)
        {
            return [.. sessions.Where(session => jid.HasBare(session.BareJid) && session.IsAvailable && session.Priority >= 0)];
        }
    }

    /// <summary>Records a stream's presence (RFC 6121 4.7.2.3 and 4.5).</summary>
    /// <param name="session">The stream.</param>
    /// <param name="isAvailable">Whether it is available.</param>
    /// <param name="priority">Its priority, -128 to 127.</param>
    public void SetPresence(XmppBoundSession session, bool isAvailable, int priority)
    {
        lock (gate)
        {
            session.IsAvailable = isAvailable;
            session.Priority = priority;
        }
    }
}
