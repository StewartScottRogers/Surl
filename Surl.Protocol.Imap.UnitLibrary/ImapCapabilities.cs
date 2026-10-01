using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Protocol.Imap;

/// <summary>
/// The capability list per state (ADR-0055, decision 2), as the greeting and <c>CAPABILITY</c>
/// send it.
/// </summary>
internal static class ImapCapabilities
{
    /// <summary>
    /// The list for a connection in this state.
    /// </summary>
    /// <param name="loginOffer">What the policy offers on the connection, or
    /// <see langword="null"/> once the session is logged in.</param>
    /// <param name="maxUploadBytes"><c>--max-filesize</c>; 0 means no limit, and no <c>APPENDLIMIT</c>.</param>
    /// <param name="canUpgrade">Whether the connection is plaintext on a server that can upgrade
    /// it, so <c>STARTTLS</c> is offered before a login.</param>
    /// <returns>The capabilities, space-separated.</returns>
    public static string List(MailLoginOffer? loginOffer, long maxUploadBytes, bool canUpgrade)
    {
        List<string> capabilities = ["IMAP4rev1"];
        if (loginOffer is not null)
        {
            capabilities.Add("SASL-IR");
        }

        capabilities.AddRange(["UIDPLUS", "UNSELECT", "NAMESPACE", "CHILDREN", "ID", "MOVE"]);
        if (maxUploadBytes > 0)
        {
            capabilities.Add("APPENDLIMIT=" + maxUploadBytes.ToString(CultureInfo.InvariantCulture));
        }

        if (loginOffer is not null)
        {
            AddLoginCapabilities(capabilities, loginOffer, canUpgrade);
        }

        return string.Join(' ', capabilities);
    }

    // STARTTLS, LOGINDISABLED and AUTH=, which describe commands allowed only before a login.
    private static void AddLoginCapabilities(List<string> capabilities, MailLoginOffer loginOffer, bool canUpgrade)
    {
        if (canUpgrade)
        {
            capabilities.Add("STARTTLS");
        }

        if (!loginOffer.IsClearPasswordLoginOffered)
        {
            capabilities.Add("LOGINDISABLED");
        }

        capabilities.AddRange(loginOffer.SaslMechanisms.Select(mechanism => "AUTH=" + mechanism));
    }
}
