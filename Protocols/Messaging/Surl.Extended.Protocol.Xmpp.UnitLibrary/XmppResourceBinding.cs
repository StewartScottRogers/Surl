using System.Xml.Linq;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The stanzas of a stream that has logged in and not yet bound a resource (RFC 6120 sections 7
/// and 8.2.3; ADR-0114 decision 4): the bind request, answered with the full JID; any other
/// stanza ends the stream with <c>not-authorized</c> (7.1). Every IQ needs an <c>id</c> and a
/// known <c>type</c>, or the stream ends with <c>invalid-xml</c> (4.9.3.11). Once bound,
/// <see cref="XmppStanzaRouter"/> takes the stream's stanzas.
/// </summary>
/// <param name="bareJid">The bare JID the stream logged in as.</param>
/// <param name="context">The exchange: its log.</param>
/// <param name="randomSource">Where a generated resource's digits come from.</param>
/// <param name="write">Writes text to the client.</param>
/// <param name="tryBind">Registers the stream under a resource, taking over a stream that holds it; <see langword="false"/> when the account has too many resources bound.</param>
internal sealed class XmppResourceBinding(
    string bareJid, ExchangeContext context, IXmppRandomSource randomSource, Func<string, Task> write, Func<string, bool> tryBind)
{
    private static readonly XName Iq = XNamespace.Get(XmppNamespaces.Client) + "iq";

    private static readonly XName BindRequest = XNamespace.Get(XmppNamespaces.Bind) + "bind";

    private static readonly XName Resource = XNamespace.Get(XmppNamespaces.Bind) + "resource";

    /// <summary>The bare JID the stream logged in as.</summary>
    public string BareJid { get; } = bareJid;

    /// <summary>The resource the stream is bound to; <see langword="null"/> until a bind succeeds.</summary>
    public string? BoundResource { get; private set; }

    /// <summary>
    /// Answers one stanza (<c>message</c>, <c>presence</c> or <c>iq</c> in <c>jabber:client</c>)
    /// before a resource is bound.
    /// </summary>
    /// <param name="stanza">The stanza.</param>
    /// <returns>A task that completes once the answer is written.</returns>
    /// <exception cref="XmppStreamErrorException">The stanza ends the stream.</exception>
    public Task AnswerAsync(XElement stanza)
    {
        var id = stanza.Name == Iq ? RequireIqShape(stanza) : null;

        return id is not null && IsBindSet(stanza)
            ? BindAsync(id, stanza.Elements().Single())
            : throw new XmppStreamErrorException(
                XmppStreamErrors.NotAuthorized, $"A {stanza.Name.LocalName} stanza arrived before a resource was bound");
    }

    /// <summary>
    /// Checks an IQ's shape (RFC 6120 8.2.3): an <c>id</c> and one of the four types.
    /// </summary>
    /// <param name="iq">The IQ.</param>
    /// <returns>Its <c>id</c>.</returns>
    /// <exception cref="XmppStreamErrorException">It has no <c>id</c> or an unknown <c>type</c> (<c>invalid-xml</c>).</exception>
    public static string RequireIqShape(XElement iq)
    {
        var id = (string?)iq.Attribute("id");
        var type = (string?)iq.Attribute("type");

        return id is not null && type is "get" or "set" or "result" or "error"
            ? id
            : throw new XmppStreamErrorException(XmppStreamErrors.InvalidXml, "An IQ had no id or no known type");
    }

    private static bool IsBindSet(XElement iq) =>
        (string?)iq.Attribute("type") == "set" && iq.Elements().Count() == 1 && iq.Elements().Single().Name == BindRequest;

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

        if (!tryBind(resource))
        {
            context.Log.Note($"{BareJid} already has {XmppSessionRegistry.MaxResourcesPerBareJid} resources bound; answered resource-constraint.");
            await write(XmppSaslText.IqError(id, "wait", "resource-constraint"));
            return;
        }

        BoundResource = resource;
        var fullJid = $"{BareJid}/{resource}";
        context.Log.Note($"Bound {fullJid}.");
        await write(XmppSaslText.BindResult(id, fullJid));
    }
}
