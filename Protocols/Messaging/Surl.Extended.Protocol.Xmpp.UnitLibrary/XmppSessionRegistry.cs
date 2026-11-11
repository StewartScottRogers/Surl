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

    /// <summary>The most resources one bare JID may have bound at once (ADR-0114 decision 4).</summary>
    public const int MaxResourcesPerBareJid = 10;

    /// <summary>
    /// Adds a stream once its resource is bound (RFC 6120 7.7.2.2; ADR-0114 decision 4): a stream
    /// already bound to the same bare JID and resource is taken over, and an account with
    /// <see cref="MaxResourcesPerBareJid"/> other resources bound is refused.
    /// </summary>
    /// <param name="session">The stream to bind.</param>
    /// <param name="displaced">The stream that held the resource, now removed, or <see langword="null"/>.</param>
    /// <returns><see langword="false"/> when the account is at its cap and the stream was not added.</returns>
    public bool TryAdd(XmppBoundSession session, out XmppBoundSession? displaced)
    {
        lock (gate)
        {
            var sameAccount = sessions.Where(other => string.Equals(other.BareJid, session.BareJid, StringComparison.OrdinalIgnoreCase)).ToList();
            displaced = sameAccount.FirstOrDefault(other => other.Resource == session.Resource);
            if (displaced is null && sameAccount.Count >= MaxResourcesPerBareJid)
            {
                return false;
            }

            if (displaced is not null)
            {
                sessions.Remove(displaced);
            }

            sessions.Add(session);
            return true;
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
