using System.Xml.Linq;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The stanzas of a stream that has logged in (RFC 6120 sections 7 and 8.2.3; ADR-0114 decision
/// 4): the bind request, answered with the full JID; before it, any other stanza ends the stream
/// with <c>not-authorized</c> (7.1); after it, a second bind is <c>not-allowed</c>, RFC 3921's
/// legacy session request an empty result, and any other IQ <c>get</c> or <c>set</c>
/// <c>service-unavailable</c> until BL-539 routes stanzas and answers IQs. Every IQ needs an
/// <c>id</c> and a known <c>type</c>, or the stream ends with <c>invalid-xml</c> (4.9.3.11).
/// </summary>
/// <param name="bareJid">The bare JID the stream logged in as.</param>
/// <param name="context">The exchange: its log.</param>
/// <param name="randomSource">Where a generated resource's digits come from.</param>
/// <param name="write">Writes text to the client.</param>
internal sealed class XmppResourceBinding(string bareJid, ExchangeContext context, IXmppRandomSource randomSource, Func<string, Task> write)
{
    private static readonly XName Iq = XNamespace.Get(XmppNamespaces.Client) + "iq";

    private static readonly XName BindRequest = XNamespace.Get(XmppNamespaces.Bind) + "bind";

    private static readonly XName Resource = XNamespace.Get(XmppNamespaces.Bind) + "resource";

    private static readonly XName SessionRequest = XNamespace.Get(XmppNamespaces.Session) + "session";

    /// <summary>The full JID the stream is bound to; <see langword="null"/> until a bind succeeds.</summary>
    public string? FullJid { get; private set; }

    /// <summary>
    /// Answers one stanza (<c>message</c>, <c>presence</c> or <c>iq</c> in <c>jabber:client</c>).
    /// </summary>
    /// <param name="stanza">The stanza.</param>
    /// <returns>A task that completes once the answer, if any, is written.</returns>
    /// <exception cref="XmppStreamErrorException">The stanza ends the stream.</exception>
    public Task AnswerAsync(XElement stanza)
    {
        var id = stanza.Name == Iq ? RequireIqShape(stanza) : null;
        if (FullJid is null)
        {
            return id is not null && IsSetOf(stanza, BindRequest)
                ? BindAsync(id, stanza.Elements().Single())
                : throw new XmppStreamErrorException(
                    XmppStreamErrors.NotAuthorized, $"A {stanza.Name.LocalName} stanza arrived before a resource was bound");
        }

        return id is null ? NoteUnroutedAsync(stanza) : AnswerBoundIqAsync(stanza, id);
    }

    // RFC 6120 8.2.3: an IQ has an id and one of four types.
    private static string RequireIqShape(XElement iq)
    {
        var id = (string?)iq.Attribute("id");
        var type = (string?)iq.Attribute("type");

        return id is not null && type is "get" or "set" or "result" or "error"
            ? id
            : throw new XmppStreamErrorException(XmppStreamErrors.InvalidXml, "An IQ had no id or no known type");
    }

    private static bool IsSetOf(XElement iq, XName request) =>
        (string?)iq.Attribute("type") == "set" && iq.Elements().Count() == 1 && iq.Elements().Single().Name == request;

    // RFC 6120 7.6 and 7.7: the client's resource when it is a resourcepart, else bad-request; a
    // generated one when the client named none.
    private async Task BindAsync(string id, XElement bind)
    {
        var asked = bind.Element(Resource);
        var resource = asked is null ? XmppJids.GeneratedResourcePrefix + randomSource.NextHexDigits(XmppJids.RandomHexDigits) : asked.Value;
        if (!XmppJids.IsValidResourcepart(resource))
        {
            context.Log.Note("A bind asked for a resource that is not a resourcepart; answered bad-request.");
            await write(XmppSaslText.IqError(id, "modify", "bad-request"));
            return;
        }

        FullJid = $"{bareJid}/{resource}";
        context.Log.Note($"Bound {FullJid}.");
        await write(XmppSaslText.BindResult(id, FullJid));
    }

    private async Task AnswerBoundIqAsync(XElement iq, string id)
    {
        var type = (string)iq.Attribute("type")!;
        if (type is "result" or "error")
        {
            return;
        }

        if (IsSetOf(iq, BindRequest))
        {
            context.Log.Note("A second bind arrived on a bound stream; answered not-allowed.");
            await write(XmppSaslText.IqError(id, "cancel", "not-allowed"));
        }
        else if (IsSetOf(iq, SessionRequest))
        {
            await write(XmppSaslText.EmptyResult(id));
        }
        else
        {
            await write(XmppSaslText.IqError(id, "cancel", "service-unavailable"));
        }
    }

    private Task NoteUnroutedAsync(XElement stanza)
    {
        context.Log.Note($"A {stanza.Name.LocalName} stanza arrived; stanzas are not routed yet, so it was dropped.");
        return Task.CompletedTask;
    }
}
