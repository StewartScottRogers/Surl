using System.Formats.Asn1;

namespace Surl.Kerberos;

/// <summary>
/// The parts of RFC 4120 section 5.5.1's decrypted <c>Authenticator</c> surl checks (ADR-0057
/// decision 4, step 5) and keeps: the client, the checksum, the client's time, the subkey and the
/// sequence number. <c>authorization-data</c> is read past.
/// </summary>
/// <param name="Client">The <c>cname</c>, with its <c>crealm</c>.</param>
/// <param name="ChecksumType">The <c>cksum.cksumtype</c>, or <see langword="null" /> when <c>cksum</c> is absent.</param>
/// <param name="Checksum">The <c>cksum.checksum</c>, empty when <c>cksum</c> is absent.</param>
/// <param name="ClientTime">The <c>ctime</c>, whole seconds.</param>
/// <param name="ClientMicroseconds">The <c>cusec</c>, 0 to 999999.</param>
/// <param name="Subkey">The <c>subkey</c>, or <see langword="null" /> when absent.</param>
/// <param name="SequenceNumber">The <c>seq-number</c>, 0 when absent.</param>
internal sealed record KerberosAuthenticatorPart(
    KerberosPrincipalName Client,
    int? ChecksumType,
    byte[] Checksum,
    DateTimeOffset ClientTime,
    int ClientMicroseconds,
    KerberosEncryptionKey? Subkey,
    uint SequenceNumber)
{
    private const int MaximumMicroseconds = 999999;

    /// <summary>Gets the client's time to the microsecond: <c>ctime</c> plus <c>cusec</c>.</summary>
    public DateTimeOffset PreciseClientTime => ClientTime.AddTicks(ClientMicroseconds * TimeSpan.TicksPerMicrosecond);

    /// <summary>
    /// Reads <c>Authenticator ::= [APPLICATION 2] SEQUENCE { authenticator-vno [0], crealm [1],
    /// cname [2], cksum [3] OPTIONAL, cusec [4], ctime [5], subkey [6] OPTIONAL,
    /// seq-number [7] OPTIONAL, authorization-data [8] OPTIONAL }</c>.
    /// </summary>
    /// <param name="bytes">The decrypted DER.</param>
    /// <returns>The authenticator.</returns>
    /// <exception cref="AsnContentException">The bytes are not such an <c>Authenticator</c>.</exception>
    public static KerberosAuthenticatorPart Read(ReadOnlyMemory<byte> bytes)
    {
        AsnReader fields = KerberosDer.ReadApplicationSequence(bytes, 2);
        KerberosApRequest.RequireValue(KerberosDer.ReadInt32Field(fields, 0), 5);
        string clientRealm = KerberosDer.ReadStringField(fields, 1);
        KerberosPrincipalName client = KerberosDer.ReadPrincipalNameField(fields, 2, clientRealm);
        (int? checksumType, byte[] checksum) = ReadOptionalChecksum(fields);
        int clientMicroseconds = KerberosDer.ReadInt32Field(fields, 4);
        if (clientMicroseconds is < 0 or > MaximumMicroseconds)
        {
            throw new AsnContentException("cusec is out of range.");
        }

        DateTimeOffset clientTime = KerberosDer.ReadTimeField(fields, 5);
        KerberosEncryptionKey? subkey = KerberosDer.HasField(fields, 6) ? KerberosEncryptionKey.ReadField(fields, 6) : null;
        uint sequenceNumber = KerberosDer.ReadOptionalUInt32Field(fields, 7) ?? 0;
        KerberosDer.SkipOptionalField(fields, 8);
        fields.ThrowIfNotEmpty();
        return new KerberosAuthenticatorPart(client, checksumType, checksum, clientTime, clientMicroseconds, subkey, sequenceNumber);
    }

    // Checksum ::= SEQUENCE { cksumtype [0] Int32, checksum [1] OCTET STRING }
    private static (int? ChecksumType, byte[] Checksum) ReadOptionalChecksum(AsnReader fields)
    {
        if (!KerberosDer.HasField(fields, 3))
        {
            return (null, []);
        }

        AsnReader field = KerberosDer.ReadField(fields, 3);
        AsnReader checksum = field.ReadSequence();
        field.ThrowIfNotEmpty();
        int checksumType = KerberosDer.ReadInt32Field(checksum, 0);
        byte[] value = KerberosDer.ReadOctetStringField(checksum, 1);
        checksum.ThrowIfNotEmpty();
        return (checksumType, value);
    }
}
