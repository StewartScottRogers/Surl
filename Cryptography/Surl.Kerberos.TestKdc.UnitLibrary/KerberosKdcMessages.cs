using System.Formats.Asn1;

namespace Surl.Kerberos.TestKdc;

/// <summary>
/// Writes the RFC 4120 section 5 messages the test KDC sends: <c>Ticket</c> and its
/// <c>EncTicketPart</c>, the <c>AS-REP</c> and <c>TGS-REP</c> with their <c>EncKDCRepPart</c>, the
/// <c>KRB-ERROR</c>, and the <c>ETYPE-INFO2</c> and <c>METHOD-DATA</c> pre-authentication hints.
/// </summary>
internal static class KerberosKdcMessages
{
    /// <summary>The <c>[APPLICATION n]</c> and <c>msg-type</c> of an <c>AS-REP</c>.</summary>
    public const int AsReplyMessageType = 11;

    /// <summary>The <c>[APPLICATION n]</c> and <c>msg-type</c> of a <c>TGS-REP</c>.</summary>
    public const int TgsReplyMessageType = 13;

    /// <summary>The <c>[APPLICATION n]</c> of an <c>EncASRepPart</c>.</summary>
    public const int EncAsReplyPartTag = 25;

    /// <summary>The <c>[APPLICATION n]</c> of an <c>EncTGSRepPart</c>.</summary>
    public const int EncTgsReplyPartTag = 26;

    /// <summary>RFC 4120 section 7.5.2's <c>PA-ENC-TIMESTAMP</c>.</summary>
    public const int EncryptedTimestampPreAuthenticationType = 2;

    /// <summary>RFC 4120 section 7.5.2's <c>PA-ETYPE-INFO2</c>.</summary>
    public const int EncryptionTypeInfo2PreAuthenticationType = 19;

    private const int ProtocolVersion = 5;
    private const int ErrorMessageType = 30;
    private const int DomainX500CompressTransitedType = 1;

    /// <summary>
    /// Writes <c>Ticket ::= [APPLICATION 1] SEQUENCE { tkt-vno [0], realm [1], sname [2], enc-part [3] }</c>.
    /// </summary>
    /// <param name="server">The ticket's server, with its realm.</param>
    /// <param name="encryptionType">The enctype of the server key the ticket is encrypted under.</param>
    /// <param name="keyVersionNumber">That key's version number.</param>
    /// <param name="cipherText">The encrypted <c>EncTicketPart</c>.</param>
    /// <returns>The ticket's DER.</returns>
    public static byte[] WriteTicket(KerberosPrincipalName server, KerberosEncryptionType encryptionType, uint keyVersionNumber, byte[] cipherText)
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence(KerberosDer.ApplicationTag(1)))
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteIntegerField(writer, 0, ProtocolVersion);
            KerberosDerWriter.WriteStringField(writer, 1, server.Realm);
            KerberosDerWriter.WritePrincipalNameField(writer, 2, server);
            KerberosDerWriter.WriteEncryptedDataField(writer, 3, encryptionType, keyVersionNumber, cipherText);
        }

        return writer.Encode();
    }

    /// <summary>
    /// Writes <c>EncTicketPart ::= [APPLICATION 3] SEQUENCE { flags [0], key [1], crealm [2],
    /// cname [3], transited [4], authtime [5], starttime [6], endtime [7] }</c>, with an empty
    /// <c>DOMAIN-X500-COMPRESS</c> transited path: the realm is the only one.
    /// </summary>
    /// <param name="ticket">What the ticket says.</param>
    /// <returns>The DER to encrypt under the server's key.</returns>
    public static byte[] WriteEncTicketPart(KerberosIssuedTicket ticket)
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence(KerberosDer.ApplicationTag(3)))
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteFlagsField(writer, 0, ticket.Flags);
            KerberosDerWriter.WriteEncryptionKeyField(writer, 1, ticket.SessionKeyType, ticket.SessionKey);
            KerberosDerWriter.WriteStringField(writer, 2, ticket.Client.Realm);
            KerberosDerWriter.WritePrincipalNameField(writer, 3, ticket.Client);
            using (KerberosDerWriter.PushField(writer, 4))
            using (writer.PushSequence())
            {
                KerberosDerWriter.WriteIntegerField(writer, 0, DomainX500CompressTransitedType);
                KerberosDerWriter.WriteOctetStringField(writer, 1, []);
            }

            KerberosDerWriter.WriteTimeField(writer, 5, ticket.AuthTime);
            KerberosDerWriter.WriteTimeField(writer, 6, ticket.StartTime);
            KerberosDerWriter.WriteTimeField(writer, 7, ticket.EndTime);
        }

        return writer.Encode();
    }

    /// <summary>
    /// Writes <c>EncKDCRepPart ::= SEQUENCE { key [0], last-req [1], nonce [2], flags [4],
    /// authtime [5], starttime [6], endtime [7], srealm [9], sname [10] }</c> under
    /// <c>[APPLICATION 25]</c> (AS) or <c>[APPLICATION 26]</c> (TGS), with one <c>last-req</c>
    /// entry of type 0 at the authentication time.
    /// </summary>
    /// <param name="applicationTag">25 or 26.</param>
    /// <param name="ticket">The ticket the reply carries.</param>
    /// <param name="nonce">The request's nonce.</param>
    /// <returns>The DER to encrypt under the reply key.</returns>
    public static byte[] WriteEncKdcReplyPart(int applicationTag, KerberosIssuedTicket ticket, uint nonce)
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence(KerberosDer.ApplicationTag(applicationTag)))
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteEncryptionKeyField(writer, 0, ticket.SessionKeyType, ticket.SessionKey);
            using (KerberosDerWriter.PushField(writer, 1))
            using (writer.PushSequence())
            using (writer.PushSequence())
            {
                KerberosDerWriter.WriteIntegerField(writer, 0, 0);
                KerberosDerWriter.WriteTimeField(writer, 1, ticket.AuthTime);
            }

            KerberosDerWriter.WriteIntegerField(writer, 2, nonce);
            KerberosDerWriter.WriteFlagsField(writer, 4, ticket.Flags);
            KerberosDerWriter.WriteTimeField(writer, 5, ticket.AuthTime);
            KerberosDerWriter.WriteTimeField(writer, 6, ticket.StartTime);
            KerberosDerWriter.WriteTimeField(writer, 7, ticket.EndTime);
            KerberosDerWriter.WriteStringField(writer, 9, ticket.Server.Realm);
            KerberosDerWriter.WritePrincipalNameField(writer, 10, ticket.Server);
        }

        return writer.Encode();
    }

    /// <summary>
    /// Writes <c>KDC-REP ::= SEQUENCE { pvno [0], msg-type [1], padata [2] OPTIONAL, crealm [3],
    /// cname [4], ticket [5], enc-part [6] }</c> under <c>[APPLICATION 11]</c> or <c>[APPLICATION 13]</c>.
    /// </summary>
    /// <param name="messageType">11 (AS-REP) or 13 (TGS-REP).</param>
    /// <param name="preAuthenticationData">The <c>padata</c>; left out when empty.</param>
    /// <param name="client">The client, with its realm.</param>
    /// <param name="ticket">The ticket's DER.</param>
    /// <param name="replyEncryptionType">The enctype of the reply key.</param>
    /// <param name="encryptedPart">The encrypted <c>EncKDCRepPart</c>.</param>
    /// <returns>The reply's DER.</returns>
    public static byte[] WriteKdcReply(
        int messageType,
        IReadOnlyList<(int Type, byte[] Value)> preAuthenticationData,
        KerberosPrincipalName client,
        byte[] ticket,
        KerberosEncryptionType replyEncryptionType,
        byte[] encryptedPart)
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence(KerberosDer.ApplicationTag(messageType)))
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteIntegerField(writer, 0, ProtocolVersion);
            KerberosDerWriter.WriteIntegerField(writer, 1, messageType);
            if (preAuthenticationData.Count > 0)
            {
                using AsnWriter.Scope field = KerberosDerWriter.PushField(writer, 2);
                writer.WriteEncodedValue(WriteMethodData(preAuthenticationData));
            }

            KerberosDerWriter.WriteStringField(writer, 3, client.Realm);
            KerberosDerWriter.WritePrincipalNameField(writer, 4, client);
            using (KerberosDerWriter.PushField(writer, 5))
            {
                writer.WriteEncodedValue(ticket);
            }

            KerberosDerWriter.WriteEncryptedDataField(writer, 6, replyEncryptionType, null, encryptedPart);
        }

        return writer.Encode();
    }

    /// <summary>
    /// Writes <c>KRB-ERROR ::= [APPLICATION 30] SEQUENCE { pvno [0], msg-type [1], stime [4],
    /// susec [5], error-code [6], crealm [7] OPTIONAL, cname [8] OPTIONAL, realm [9], sname [10],
    /// e-text [11], e-data [12] OPTIONAL }</c>.
    /// </summary>
    /// <param name="now">The KDC's time, for <c>stime</c> and <c>susec</c>.</param>
    /// <param name="refusal">The error code, its text and its data.</param>
    /// <param name="client">The request's client, or <see langword="null" /> to leave <c>crealm</c> and <c>cname</c> out.</param>
    /// <param name="server">The request's server, or the ticket-granting service when it named none.</param>
    /// <returns>The error's DER.</returns>
    public static byte[] WriteError(DateTimeOffset now, KerberosKdcRefusalException refusal, KerberosPrincipalName? client, KerberosPrincipalName server)
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence(KerberosDer.ApplicationTag(ErrorMessageType)))
        using (writer.PushSequence())
        {
            KerberosDerWriter.WriteIntegerField(writer, 0, ProtocolVersion);
            KerberosDerWriter.WriteIntegerField(writer, 1, ErrorMessageType);
            KerberosDerWriter.WriteTimeField(writer, 4, now);
            KerberosDerWriter.WriteIntegerField(writer, 5, (now.UtcTicks % TimeSpan.TicksPerSecond) / TimeSpan.TicksPerMicrosecond);
            KerberosDerWriter.WriteIntegerField(writer, 6, (int)refusal.ErrorCode);
            if (client is not null)
            {
                KerberosDerWriter.WriteStringField(writer, 7, client.Realm);
                KerberosDerWriter.WritePrincipalNameField(writer, 8, client);
            }

            KerberosDerWriter.WriteStringField(writer, 9, server.Realm);
            KerberosDerWriter.WritePrincipalNameField(writer, 10, server);
            KerberosDerWriter.WriteStringField(writer, 11, refusal.Message);
            if (refusal.ErrorData is byte[] errorData)
            {
                KerberosDerWriter.WriteOctetStringField(writer, 12, errorData);
            }
        }

        return writer.Encode();
    }

    /// <summary>
    /// Writes <c>ETYPE-INFO2 ::= SEQUENCE OF ETYPE-INFO2-ENTRY</c>, one
    /// <c>SEQUENCE { etype [0], salt [1] }</c> per enctype, with no <c>s2kparams</c>: every key
    /// uses its enctype's default iteration count.
    /// </summary>
    /// <param name="encryptionTypes">The enctypes, in the client's order.</param>
    /// <param name="salt">The user's salt.</param>
    /// <returns>The DER, a <c>PA-ETYPE-INFO2</c>'s value.</returns>
    public static byte[] WriteEncryptionTypeInfo2(IEnumerable<KerberosEncryptionType> encryptionTypes, string salt)
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence())
        {
            foreach (KerberosEncryptionType encryptionType in encryptionTypes)
            {
                using AsnWriter.Scope entry = writer.PushSequence();
                KerberosDerWriter.WriteIntegerField(writer, 0, (int)encryptionType);
                KerberosDerWriter.WriteStringField(writer, 1, salt);
            }
        }

        return writer.Encode();
    }

    /// <summary>Writes <c>METHOD-DATA ::= SEQUENCE OF PA-DATA</c>.</summary>
    /// <param name="preAuthenticationData">The <c>padata-type</c> and <c>padata-value</c> pairs.</param>
    /// <returns>The DER.</returns>
    public static byte[] WriteMethodData(IEnumerable<(int Type, byte[] Value)> preAuthenticationData)
    {
        AsnWriter writer = KerberosDerWriter.NewWriter();
        using (writer.PushSequence())
        {
            foreach ((int type, byte[] value) in preAuthenticationData)
            {
                KerberosDerWriter.WritePreAuthenticationData(writer, type, value);
            }
        }

        return writer.Encode();
    }
}
