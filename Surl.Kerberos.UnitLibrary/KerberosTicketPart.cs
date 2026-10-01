using System.Formats.Asn1;

namespace Surl.Kerberos;

/// <summary>
/// The parts of RFC 4120 section 5.3's <c>EncTicketPart</c> surl checks (ADR-0057 decision 4,
/// step 4): the <c>invalid</c> flag, the session key, the client and the validity times.
/// <c>transited</c>, <c>renew-till</c>, <c>caddr</c> and <c>authorization-data</c> are read past,
/// never checked.
/// </summary>
/// <param name="IsInvalid">Whether <c>flags</c> has <c>invalid</c> (bit 7).</param>
/// <param name="SessionKey">The <c>key</c>.</param>
/// <param name="Client">The <c>cname</c>, with its <c>crealm</c>.</param>
/// <param name="AuthTime">The <c>authtime</c>.</param>
/// <param name="StartTime">The <c>starttime</c>, or <see langword="null" /> when absent.</param>
/// <param name="EndTime">The <c>endtime</c>.</param>
internal sealed record KerberosTicketPart(
    bool IsInvalid,
    KerberosEncryptionKey SessionKey,
    KerberosPrincipalName Client,
    DateTimeOffset AuthTime,
    DateTimeOffset? StartTime,
    DateTimeOffset EndTime)
{
    private const int InvalidBit = 7;

    /// <summary>
    /// Reads <c>EncTicketPart ::= [APPLICATION 3] SEQUENCE { flags [0], key [1], crealm [2],
    /// cname [3], transited [4], authtime [5], starttime [6] OPTIONAL, endtime [7],
    /// renew-till [8] OPTIONAL, caddr [9] OPTIONAL, authorization-data [10] OPTIONAL }</c>.
    /// </summary>
    /// <param name="bytes">The decrypted DER.</param>
    /// <returns>The ticket's encrypted part.</returns>
    /// <exception cref="AsnContentException">The bytes are not such an <c>EncTicketPart</c>.</exception>
    public static KerberosTicketPart Read(ReadOnlyMemory<byte> bytes)
    {
        AsnReader fields = KerberosDer.ReadApplicationSequence(bytes, 3);
        byte[] flags = KerberosDer.ReadFlagsField(fields, 0);
        KerberosEncryptionKey sessionKey = KerberosEncryptionKey.ReadField(fields, 1);
        string clientRealm = KerberosDer.ReadStringField(fields, 2);
        KerberosPrincipalName client = KerberosDer.ReadPrincipalNameField(fields, 3, clientRealm);
        KerberosDer.ReadField(fields, 4);
        DateTimeOffset authTime = KerberosDer.ReadTimeField(fields, 5);
        DateTimeOffset? startTime = KerberosDer.ReadOptionalTimeField(fields, 6);
        DateTimeOffset endTime = KerberosDer.ReadTimeField(fields, 7);
        KerberosDer.SkipOptionalField(fields, 8);
        KerberosDer.SkipOptionalField(fields, 9);
        KerberosDer.SkipOptionalField(fields, 10);
        fields.ThrowIfNotEmpty();
        return new KerberosTicketPart(KerberosDer.IsFlagSet(flags, InvalidBit), sessionKey, client, authTime, startTime, endTime);
    }
}
