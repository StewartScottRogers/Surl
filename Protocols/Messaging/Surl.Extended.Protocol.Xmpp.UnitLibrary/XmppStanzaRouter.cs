using System.Globalization;
using System.Xml.Linq;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The stanzas of a bound stream (RFC 6120 sections 8 and 10; RFC 6121 sections 4.7 and 8.5;
/// XEP-0030; XEP-0199; ADR-0114 decision 5). A <c>from</c> that is neither the stream's full nor
/// its bare JID ends the stream with <c>invalid-from</c>; every routed stanza carries the full
/// JID as <c>from</c>. A <c>to</c> that is not a JID is <c>jid-malformed</c>, one on a domain not
/// served <c>remote-server-not-found</c>, and none means the sender's own bare JID.
/// </summary>
/// <remarks>
/// <para>
/// Messages go to a bound full JID's stream; to a bare JID, or an unbound full one, <c>normal</c>
/// and <c>chat</c> (and an unknown type) go to every available stream of the account with a
/// non-negative priority, and with none are <c>service-unavailable</c> whether or not the account
/// exists; <c>headline</c> with none is dropped, <c>groupchat</c> is <c>service-unavailable</c>, and
/// <c>error</c> is never answered. To a served domain, <c>normal</c> and <c>chat</c> are
/// <c>service-unavailable</c>, the rest dropped.
/// </para>
/// <para>
/// Undirected presence sets the stream's priority (-128 to 127, else <c>bad-request</c>), and
/// <c>unavailable</c> takes it out of bare-JID delivery; directed presence is dropped.
/// </para>
/// <para>
/// An IQ <c>get</c> or <c>set</c> needs exactly one child, else <c>bad-request</c>. To a bound
/// full JID it is delivered, and the reply routed back; otherwise the server answers: a second
/// bind is <c>not-allowed</c>, the legacy session an empty result, a served domain's disco#info,
/// disco#items and ping, an account's disco#info and disco#items, and one's own empty roster; a
/// disco <c>node</c> is <c>item-not-found</c>; anything else <c>service-unavailable</c>. An IQ
/// <c>result</c> or <c>error</c> to anything but a bound full JID is dropped.
/// </para>
/// </remarks>
/// <param name="session">The stream's own bound session.</param>
/// <param name="registry">The server's bound streams.</param>
/// <param name="domains">The domains served, normalized.</param>
/// <param name="context">The exchange: its log.</param>
/// <param name="write">Writes text to the stream's own client.</param>
internal sealed class XmppStanzaRouter(
    XmppBoundSession session, XmppSessionRegistry registry, IReadOnlyList<string> domains, ExchangeContext context, Func<string, Task> write)
{
    private const string Cancel = "cancel";

    private const string Modify = "modify";

    private const string ServiceUnavailable = "service-unavailable";

    private static readonly XNamespace Client = XmppNamespaces.Client;

    private static readonly XName BindRequest = XNamespace.Get(XmppNamespaces.Bind) + "bind";

    private static readonly XName SessionRequest = XNamespace.Get(XmppNamespaces.Session) + "session";

    private static readonly XName PingRequest = XNamespace.Get(XmppStanzaText.Ping) + "ping";

    private static readonly XName RosterQuery = XNamespace.Get(XmppStanzaText.Roster) + "query";

    private static readonly IqAnswer Unavailable = new(string.Empty, ServiceUnavailable);

    private static readonly IqAnswer EmptyResult = new(string.Empty, null);

    private readonly XmppJid ownBareJid = XmppJid.Parse(session.BareJid)!;

    /// <summary>
    /// Answers or routes one stanza (<c>message</c>, <c>presence</c> or <c>iq</c> in <c>jabber:client</c>).
    /// </summary>
    /// <param name="stanza">The stanza.</param>
    /// <returns>A task that completes once the answer, if any, is written and any delivery made.</returns>
    /// <exception cref="XmppStreamErrorException">The stanza ends the stream (<c>invalid-xml</c> or <c>invalid-from</c>).</exception>
    public Task AnswerAsync(XElement stanza)
    {
        var id = stanza.Name.LocalName == "iq" ? XmppResourceBinding.RequireIqShape(stanza) : null;
        RequireOwnFrom(stanza);
        var to = (string?)stanza.Attribute("to");
        var target = to is null ? ownBareJid : XmppJid.Parse(to);
        if (target is null)
        {
            return ReplyErrorAsync(stanza, Modify, "jid-malformed");
        }

        if (!domains.Contains(target.Domain))
        {
            return ReplyErrorAsync(stanza, Cancel, "remote-server-not-found");
        }

        return stanza.Name.LocalName switch
        {
            "message" => RouteMessageAsync(stanza, target),
            "presence" => AnswerPresenceAsync(stanza, isDirected: to is not null),
            _ => AnswerIqAsync(stanza, target, id!),
        };
    }

    // RFC 6120 8.1.2.1: the client may name its full or bare JID, nothing else (4.9.3.9).
    private void RequireOwnFrom(XElement stanza)
    {
        var from = (string?)stanza.Attribute("from");
        if (from is null)
        {
            return;
        }

        var jid = XmppJid.Parse(from);
        if (jid is null || !jid.HasBare(session.BareJid) || (jid.Resource is not null && jid.Resource != session.Resource))
        {
            throw new XmppStreamErrorException(XmppStreamErrors.InvalidFrom, $"A stanza named the from {from}, which is not the stream's JID");
        }
    }

    // RFC 6121 8.5.
    private Task RouteMessageAsync(XElement message, XmppJid target)
    {
        var type = (string?)message.Attribute("type") ?? "normal";
        if (target.Localpart is null)
        {
            return type is "headline" or "groupchat" or "error"
                ? DropAsync(message, "it was addressed to the server")
                : ReplyErrorAsync(message, Cancel, ServiceUnavailable);
        }

        if (registry.Bound(target) is { } bound)
        {
            return Deliver(message, [bound]);
        }

        if (type is "groupchat" or "error")
        {
            return ReplyErrorAsync(message, Cancel, ServiceUnavailable);
        }

        var recipients = registry.AvailableWithNonNegativePriority(target);
        if (recipients.Count > 0)
        {
            return Deliver(message, recipients);
        }

        return type == "headline" ? DropAsync(message, "no stream of the account is available") : ReplyErrorAsync(message, Cancel, ServiceUnavailable);
    }

    // RFC 6121 4.7.2.3 and 4.5.1; with no rosters, directed and subscription presence go nowhere (3.1.3).
    private Task AnswerPresenceAsync(XElement presence, bool isDirected)
    {
        var type = (string?)presence.Attribute("type");
        if (isDirected || type is not (null or "unavailable"))
        {
            return DropAsync(presence, "there are no rosters to route it by");
        }

        if (type is not null)
        {
            registry.SetPresence(session, isAvailable: false, session.Priority);
            context.Log.Note($"{session.FullJid} is unavailable.");
            return Task.CompletedTask;
        }

        var priority = Priority(presence);
        if (priority is null)
        {
            return ReplyErrorAsync(presence, Modify, "bad-request");
        }

        registry.SetPresence(session, isAvailable: true, priority.Value);
        context.Log.Note($"{session.FullJid} is available at priority {priority.Value}.");
        return Task.CompletedTask;
    }

    // RFC 6121 4.7.2.3: an xs:byte, 0 when absent.
    private static int? Priority(XElement presence)
    {
        var element = presence.Element(Client + "priority");
        if (element is null)
        {
            return 0;
        }

        return sbyte.TryParse(element.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var priority) ? priority : null;
    }

    // RFC 6120 8.2.3 and 10.3; RFC 6121 8.5.
    private Task AnswerIqAsync(XElement iq, XmppJid target, string id)
    {
        var bound = registry.Bound(target);
        if ((string)iq.Attribute("type")! is "result" or "error")
        {
            return bound is null ? DropAsync(iq, "an IQ reply is never answered") : Deliver(iq, [bound]);
        }

        if (iq.Elements().Count() != 1)
        {
            return ReplyErrorAsync(iq, Modify, "bad-request");
        }

        if (bound is not null)
        {
            return Deliver(iq, [bound]);
        }

        var answer = target.Resource is null ? Answer((string)iq.Attribute("type")!, iq.Elements().Single(), target) : Unavailable;
        return answer.Condition is { } condition
            ? ReplyErrorAsync(iq, Cancel, condition)
            : write(XmppStanzaText.Result(id, (string?)iq.Attribute("to"), session.FullJid, answer.Payload));
    }

    // Decision 4's second bind and legacy session wherever they are addressed, then the server's
    // answers for a domain and for an account.
    private IqAnswer Answer(string type, XElement child, XmppJid target)
    {
        if (type == "set" && child.Name == BindRequest)
        {
            context.Log.Note("A second bind arrived on a bound stream; answered not-allowed.");
            return new(string.Empty, "not-allowed");
        }

        if (type == "set" && child.Name == SessionRequest)
        {
            return EmptyResult;
        }

        return target.Localpart is null ? ServerAnswer(type, child) : AccountAnswer(type, child, target);
    }

    private static IqAnswer ServerAnswer(string type, XElement child) =>
        type == "get" && child.Name == PingRequest ? EmptyResult : Discovery(type, child, XmppStanzaText.ServerInfo);

    // RFC 6121 8.5.1 and 8.5.2.2.3: an account with no bound stream answers nothing.
    private IqAnswer AccountAnswer(string type, XElement child, XmppJid target)
    {
        if (!registry.HasBound(target))
        {
            return Unavailable;
        }

        return type == "get" && child.Name == RosterQuery && target.HasBare(session.BareJid)
            ? new(XmppStanzaText.EmptyRoster, null)
            : Discovery(type, child, XmppStanzaText.AccountInfo);
    }

    // XEP-0030 3.1, 3.2 and 4.1.
    private static IqAnswer Discovery(string type, XElement child, string info)
    {
        var space = child.Name.NamespaceName;
        if (type != "get" || child.Name.LocalName != "query" || space is not (XmppStanzaText.DiscoInfo or XmppStanzaText.DiscoItems))
        {
            return Unavailable;
        }

        if (child.Attribute("node") is not null)
        {
            return new(string.Empty, "item-not-found");
        }

        return new(space == XmppStanzaText.DiscoInfo ? info : XmppStanzaText.NoItems, null);
    }

    // Delivery queues the stanza on each recipient's connection and returns: the sender never
    // waits for, or learns of, a slow or failed write (ADR-0114 decision 7).
    private Task Deliver(XElement stanza, IReadOnlyList<XmppBoundSession> recipients)
    {
        var text = XmppStanzaText.Routed(stanza, session.FullJid);
        foreach (var recipient in recipients)
        {
            recipient.Deliver(text);
            context.Log.Note($"Routed a {stanza.Name.LocalName} from {session.FullJid} to {recipient.FullJid}.");
        }

        return Task.CompletedTask;
    }

    // RFC 6120 8.3.1: a stanza of type error never gets an error back.
    private Task ReplyErrorAsync(XElement stanza, string type, string condition)
    {
        if ((string?)stanza.Attribute("type") == "error")
        {
            return DropAsync(stanza, $"it was of type error and would have been answered {condition}");
        }

        context.Log.Note($"A {stanza.Name.LocalName} from {session.FullJid} was answered {condition}.");
        return write(XmppStanzaText.Error(stanza, session.FullJid, type, condition));
    }

    private Task DropAsync(XElement stanza, string why)
    {
        context.Log.Note($"A {stanza.Name.LocalName} from {session.FullJid} was dropped: {why}.");
        return Task.CompletedTask;
    }

    // What the server answers an IQ get or set with: a result payload, or an error condition.
    private sealed record IqAnswer(string Payload, string? Condition);
}
