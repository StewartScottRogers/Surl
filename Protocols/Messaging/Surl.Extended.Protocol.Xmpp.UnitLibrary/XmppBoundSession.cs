namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// One stream bound to a resource, as <see cref="XmppSessionRegistry"/> holds it for routing
/// (ADR-0114 decision 5): its JID, its presence, and how a stanza is written to it.
/// </summary>
/// <param name="bareJid">The bare JID the stream logged in as.</param>
/// <param name="resource">The resource it is bound to.</param>
/// <param name="tryDeliver">Writes a stanza to the stream; <see langword="false"/> when the stream could not take it.</param>
internal sealed class XmppBoundSession(string bareJid, string resource, Func<string, Task<bool>> tryDeliver)
{
    /// <summary>The bare JID the stream logged in as.</summary>
    public string BareJid { get; } = bareJid;

    /// <summary>The resource the stream is bound to.</summary>
    public string Resource { get; } = resource;

    /// <summary>The full JID, <c>bare/resource</c>.</summary>
    public string FullJid => $"{BareJid}/{Resource}";

    /// <summary>
    /// Whether the stream is available: from bind until it sends <c>&lt;presence type='unavailable'/&gt;</c>,
    /// and again once it sends presence. Changed only under <see cref="XmppSessionRegistry"/>'s lock.
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>The priority its last undirected presence named; 0 until it sends one. Changed only under the registry's lock.</summary>
    public int Priority { get; set; }

    /// <summary>
    /// Writes a routed stanza to the stream.
    /// </summary>
    /// <param name="stanza">The stanza's text.</param>
    /// <returns><see langword="true"/> when written; <see langword="false"/> when the stream's connection failed or was cancelled.</returns>
    public Task<bool> TryDeliverAsync(string stanza) => tryDeliver(stanza);
}
