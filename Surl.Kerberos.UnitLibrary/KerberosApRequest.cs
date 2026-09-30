using System.Formats.Asn1;

namespace Surl.Kerberos;

/// <summary>
/// The parts of RFC 4120 section 5.5.1's <c>KRB_AP_REQ</c> surl reads before it decrypts
/// anything: the <c>ap-options</c> flags, the ticket's server principal and its encrypted part,
/// and the encrypted authenticator.
/// </summary>
/// <param name="IsUseSessionKeyRequested">Whether <c>ap-options</c> has <c>use-session-key</c> (bit 1), user-to-user.</param>
/// <param name="IsMutualRequired">Whether <c>ap-options</c> has <c>mutual-required</c> (bit 2).</param>
/// <param name="ServerName">The ticket's <c>sname</c>, with its <c>realm</c>.</param>
/// <param name="TicketEncryptedPart">The ticket's <c>enc-part</c>.</param>
/// <param name="Authenticator">The encrypted <c>authenticator</c>.</param>
internal sealed record KerberosApRequest(
    bool IsUseSessionKeyRequested,
    bool IsMutualRequired,
    KerberosPrincipalName ServerName,
    KerberosEncryptedData TicketEncryptedPart,
    KerberosEncryptedData Authenticator)
{
    private const int ProtocolVersion = 5;
    private const int ApRequestMessageType = 14;
    private const int UseSessionKeyBit = 1;
    private const int MutualRequiredBit = 2;

    /// <summary>
    /// Reads <c>AP-REQ ::= [APPLICATION 14] SEQUENCE { pvno [0], msg-type [1], ap-options [2],
    /// ticket [3] Ticket, authenticator [4] EncryptedData }</c>, with <c>pvno</c> 5 and
    /// <c>msg-type</c> 14.
    /// </summary>
    /// <param name="bytes">The DER of the AP-REQ.</param>
    /// <returns>The AP-REQ.</returns>
    /// <exception cref="AsnContentException">The bytes are not such an AP-REQ.</exception>
    public static KerberosApRequest Read(ReadOnlyMemory<byte> bytes)
    {
        AsnReader fields = KerberosDer.ReadApplicationSequence(bytes, 14);
        RequireValue(KerberosDer.ReadInt32Field(fields, 0), ProtocolVersion);
        RequireValue(KerberosDer.ReadInt32Field(fields, 1), ApRequestMessageType);
        byte[] apOptions = KerberosDer.ReadFlagsField(fields, 2);
        (KerberosPrincipalName serverName, KerberosEncryptedData ticketEncryptedPart) = ReadTicket(KerberosDer.ReadField(fields, 3));
        KerberosEncryptedData authenticator = KerberosEncryptedData.ReadField(fields, 4);
        fields.ThrowIfNotEmpty();
        return new KerberosApRequest(
            KerberosDer.IsFlagSet(apOptions, UseSessionKeyBit),
            KerberosDer.IsFlagSet(apOptions, MutualRequiredBit),
            serverName,
            ticketEncryptedPart,
            authenticator);
    }

    /// <summary>Throws unless a fixed field holds the only value RFC 4120 allows it.</summary>
    /// <param name="value">The value read.</param>
    /// <param name="required">The value required.</param>
    /// <exception cref="AsnContentException"><paramref name="value" /> is not <paramref name="required" />.</exception>
    internal static void RequireValue(int value, int required)
    {
        if (value != required)
        {
            throw new AsnContentException($"Expected {required}, read {value}.");
        }
    }

    // Ticket ::= [APPLICATION 1] SEQUENCE { tkt-vno [0], realm [1], sname [2], enc-part [3] }
    private static (KerberosPrincipalName ServerName, KerberosEncryptedData EncryptedPart) ReadTicket(AsnReader ticketField)
    {
        AsnReader fields = KerberosDer.ReadApplicationSequence(ticketField, 1);
        RequireValue(KerberosDer.ReadInt32Field(fields, 0), ProtocolVersion);
        string realm = KerberosDer.ReadStringField(fields, 1);
        KerberosPrincipalName serverName = KerberosDer.ReadPrincipalNameField(fields, 2, realm);
        KerberosEncryptedData encryptedPart = KerberosEncryptedData.ReadField(fields, 3);
        fields.ThrowIfNotEmpty();
        return (serverName, encryptedPart);
    }
}
