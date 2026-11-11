using System.Text;

namespace Surl.Extended.Protocol.Xmpp;

/// <summary>
/// Reads the authorization identity out of a client's first SASL message (RFC 6120 section 6.3.8
/// and 6.5.6; ADR-0114 decision 3), because the policy's exchanges refuse a foreign one as a bad
/// credential and XMPP answers <c>invalid-authzid</c> instead.
/// </summary>
internal static class XmppAuthzid
{
    /// <summary>
    /// Judges a PLAIN message, <c>[authzid] NUL authcid NUL password</c> (RFC 4616). An authzid equal
    /// to the account's own bare JID is dropped from the message the exchange sees.
    /// </summary>
    /// <param name="message">The message as the client sent it.</param>
    /// <param name="domain">The stream's domain.</param>
    /// <param name="randomSource">Where an anonymous name's digits would come from; unused for a named authcid.</param>
    /// <param name="forwarded">The message the exchange should judge.</param>
    /// <returns><see langword="false"/> when the authzid is not empty, the authcid or the account's bare JID.</returns>
    public static bool IsAcceptablePlain(byte[] message, string domain, IXmppRandomSource randomSource, out byte[] forwarded)
    {
        forwarded = message;
        var first = Array.IndexOf(message, (byte)0);
        var second = first < 0 ? -1 : Array.IndexOf(message, (byte)0, first + 1);
        if (second < 0)
        {
            return true;
        }

        var authzid = Encoding.UTF8.GetString(message, 0, first);
        var authcid = Encoding.UTF8.GetString(message, first + 1, second - first - 1);
        if (authzid.Length == 0 || authzid == authcid)
        {
            return true;
        }

        if (authzid != XmppJids.BareJid(authcid, domain, randomSource))
        {
            return false;
        }

        forwarded = message[first..];
        return true;
    }

    /// <summary>
    /// Judges a SCRAM client-first message, <c>gs2-header n=user,r=nonce</c> (RFC 5802 section 7). The
    /// message cannot be rewritten, since its <c>c=</c> repeats the header, so only an <c>a=</c> equal
    /// to the user is acceptable.
    /// </summary>
    /// <param name="message">The message as the client sent it.</param>
    /// <returns><see langword="false"/> when it has an <c>a=</c> that is not the user.</returns>
    public static bool IsAcceptableScram(byte[] message)
    {
        var fields = Encoding.UTF8.GetString(message).Split(',');
        if (fields.Length < 2 || !fields[1].StartsWith("a=", StringComparison.Ordinal))
        {
            return true;
        }

        var user = fields.FirstOrDefault(field => field.StartsWith("n=", StringComparison.Ordinal));
        return user is not null && Decoded(fields[1][2..]) == Decoded(user[2..]);
    }

    private static string Decoded(string name) => name.Replace("=2C", ",", StringComparison.Ordinal).Replace("=3D", "=", StringComparison.Ordinal);
}
