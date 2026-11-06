using System.Security;
using System.Xml.Linq;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// The stanzas the server writes once a stream is bound (RFC 6120 sections 8.2.3 and 8.3;
/// XEP-0030 sections 3.1 and 4.1; ADR-0114 decision 5): IQ results, the service discovery
/// payloads, routed stanzas and stanza errors.
/// </summary>
internal static class XmppStanzaText
{
    /// <summary>The service discovery information namespace (XEP-0030 section 3).</summary>
    public const string DiscoInfo = "http://jabber.org/protocol/disco#info";

    /// <summary>The service discovery items namespace (XEP-0030 section 4).</summary>
    public const string DiscoItems = "http://jabber.org/protocol/disco#items";

    /// <summary>The XMPP ping namespace (XEP-0199).</summary>
    public const string Ping = "urn:xmpp:ping";

    /// <summary>The roster namespace (RFC 6121 section 2).</summary>
    public const string Roster = "jabber:iq:roster";

    /// <summary>A served domain's disco#info: a server of type <c>im</c>, no <c>name</c>, and the three features it answers.</summary>
    public const string ServerInfo =
        $"<query xmlns='{DiscoInfo}'><identity category='server' type='im'/>" +
        $"<feature var='{DiscoInfo}'/><feature var='{DiscoItems}'/><feature var='{Ping}'/></query>";

    /// <summary>A bound account's disco#info: a registered account answering disco#info and disco#items.</summary>
    public const string AccountInfo =
        $"<query xmlns='{DiscoInfo}'><identity category='account' type='registered'/>" +
        $"<feature var='{DiscoInfo}'/><feature var='{DiscoItems}'/></query>";

    /// <summary>An empty disco#items: there are no components or items.</summary>
    public const string NoItems = $"<query xmlns='{DiscoItems}'/>";

    /// <summary>An empty roster (RFC 6121 2.1.3): there are no rosters.</summary>
    public const string EmptyRoster = $"<query xmlns='{Roster}'/>";

    /// <summary>
    /// An IQ result (8.2.3) in XEP-0030 example 2's shape: <c>type</c>, <c>from</c> (the
    /// request's <c>to</c>, left out when it had none), <c>to</c> the requester's full JID, and <c>id</c>.
    /// </summary>
    /// <param name="id">The request's <c>id</c>.</param>
    /// <param name="from">The request's <c>to</c>, or <see langword="null"/>.</param>
    /// <param name="to">The requester's full JID.</param>
    /// <param name="payload">The result's child, or the empty string for an empty result.</param>
    /// <returns>The result IQ.</returns>
    public static string Result(string id, string? from, string to, string payload)
    {
        var fromAttribute = from is null ? string.Empty : $" from='{Escaped(from)}'";
        var head = $"<iq type='result'{fromAttribute} to='{Escaped(to)}' id='{Escaped(id)}'";

        return payload.Length == 0 ? head + "/>" : $"{head}>{payload}</iq>";
    }

    /// <summary>
    /// A stanza error (8.3): the stanza returned with <c>type='error'</c>, <c>from</c> the
    /// stanza's <c>to</c> (left out when it had none) and <c>to</c> the sender, the IQ's children
    /// kept and any other stanza's dropped, then the error with no <c>&lt;text/&gt;</c>.
    /// </summary>
    /// <param name="stanza">The stanza as the client sent it.</param>
    /// <param name="sender">The sender's full JID.</param>
    /// <param name="type">The error type: <c>modify</c> or <c>cancel</c>.</param>
    /// <param name="condition">The stanza error condition.</param>
    /// <returns>The error stanza.</returns>
    public static string Error(XElement stanza, string sender, string type, string condition)
    {
        var reply = new XElement(stanza);
        if (stanza.Name.LocalName != "iq")
        {
            reply.RemoveNodes();
        }

        reply.SetAttributeValue("type", "error");
        reply.SetAttributeValue("from", (string?)stanza.Attribute("to"));
        reply.SetAttributeValue("to", sender);
        reply.Add(new XElement(
            XNamespace.Get(XmppNamespaces.Client) + "error",
            new XAttribute("type", type),
            new XElement(XNamespace.Get(XmppNamespaces.Stanzas) + condition)));

        return reply.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// A stanza as it is routed (decision 5): as received, with <c>from</c> stamped as the
    /// sender's full JID and the default namespace <c>jabber:client</c>.
    /// </summary>
    /// <param name="stanza">The stanza as the client sent it.</param>
    /// <param name="sender">The sender's full JID.</param>
    /// <returns>The stanza's text.</returns>
    public static string Routed(XElement stanza, string sender)
    {
        var routed = new XElement(stanza);
        routed.SetAttributeValue("from", sender);

        return routed.ToString(SaveOptions.DisableFormatting);
    }

    private static string Escaped(string value) => SecurityElement.Escape(value);
}
