using System.Formats.Asn1;

namespace Surl.Kerberos.TestKdc;

/// <summary>
/// The parts of RFC 4120 section 5.4.1's <c>KDC-REQ</c> the test KDC reads, whether an
/// <c>AS-REQ</c> (<c>[APPLICATION 10]</c>) or a <c>TGS-REQ</c> (<c>[APPLICATION 12]</c>): the
/// pre-authentication data and, from <c>req-body</c>, the client, the realm, the server, the
/// requested end time, the nonce and the client's enctypes in its order of preference.
/// <c>kdc-options</c>, <c>from</c>, <c>rtime</c>, <c>addresses</c>,
/// <c>enc-authorization-data</c> and <c>additional-tickets</c> are read past.
/// </summary>
/// <param name="IsTicketGrantingRequest">Whether the request is a TGS-REQ rather than an AS-REQ.</param>
/// <param name="PreAuthenticationData">The <c>padata</c>, as <c>padata-type</c> and <c>padata-value</c> pairs; empty when absent.</param>
/// <param name="ClientName">The <c>cname</c> with the body's <c>realm</c>, or <see langword="null" /> when absent.</param>
/// <param name="ServerName">The <c>sname</c> with the body's <c>realm</c>, or <see langword="null" /> when absent.</param>
/// <param name="Till">The <c>till</c>.</param>
/// <param name="Nonce">The <c>nonce</c>.</param>
/// <param name="EncryptionTypeNumbers">The <c>etype</c> list, in the client's order.</param>
internal sealed record KerberosKdcRequest(
    bool IsTicketGrantingRequest,
    IReadOnlyList<(int Type, byte[] Value)> PreAuthenticationData,
    KerberosPrincipalName? ClientName,
    KerberosPrincipalName? ServerName,
    DateTimeOffset Till,
    uint Nonce,
    IReadOnlyList<int> EncryptionTypeNumbers)
{
    private const int AsRequestMessageType = 10;
    private const int TgsRequestMessageType = 12;

    /// <summary>Reads an AS-REQ or a TGS-REQ.</summary>
    /// <param name="bytes">The DER of the request.</param>
    /// <returns>The request.</returns>
    /// <exception cref="AsnContentException">The bytes are neither.</exception>
    public static KerberosKdcRequest Read(ReadOnlyMemory<byte> bytes)
    {
        int messageType = new AsnReader(bytes, KerberosDer.Rules).PeekTag() == KerberosDer.ApplicationTag(TgsRequestMessageType)
            ? TgsRequestMessageType
            : AsRequestMessageType;
        AsnReader fields = KerberosDer.ReadApplicationSequence(bytes, messageType);
        KerberosApRequest.RequireValue(KerberosDer.ReadInt32Field(fields, 1), 5);
        KerberosApRequest.RequireValue(KerberosDer.ReadInt32Field(fields, 2), messageType);
        IReadOnlyList<(int Type, byte[] Value)> preAuthenticationData = KerberosDer.HasField(fields, 3) ? ReadPreAuthenticationData(fields) : [];
        AsnReader bodyField = KerberosDer.ReadField(fields, 4);
        fields.ThrowIfNotEmpty();
        AsnReader body = bodyField.ReadSequence();
        bodyField.ThrowIfNotEmpty();
        return ReadBody(body, messageType == TgsRequestMessageType, preAuthenticationData);
    }

    // KDC-REQ-BODY ::= SEQUENCE { kdc-options [0], cname [1] OPTIONAL, realm [2], sname [3] OPTIONAL,
    //   from [4] OPTIONAL, till [5], rtime [6] OPTIONAL, nonce [7], etype [8], addresses [9] OPTIONAL,
    //   enc-authorization-data [10] OPTIONAL, additional-tickets [11] OPTIONAL }
    private static KerberosKdcRequest ReadBody(AsnReader body, bool isTicketGrantingRequest, IReadOnlyList<(int Type, byte[] Value)> preAuthenticationData)
    {
        KerberosDer.ReadFlagsField(body, 0);
        ReadOnlyMemory<byte>? clientNameField = KerberosDer.HasField(body, 1) ? body.ReadEncodedValue() : (ReadOnlyMemory<byte>?)null;
        string realm = KerberosDer.ReadStringField(body, 2);
        KerberosPrincipalName? serverName = KerberosDer.HasField(body, 3) ? KerberosDer.ReadPrincipalNameField(body, 3, realm) : null;
        KerberosDer.SkipOptionalField(body, 4);
        DateTimeOffset till = KerberosDer.ReadTimeField(body, 5);
        KerberosDer.SkipOptionalField(body, 6);
        uint nonce = KerberosDer.ReadOptionalUInt32Field(body, 7) ?? throw new AsnContentException("A KDC-REQ-BODY has no nonce.");
        IReadOnlyList<int> encryptionTypeNumbers = ReadEncryptionTypes(KerberosDer.ReadField(body, 8));
        KerberosDer.SkipOptionalField(body, 9);
        KerberosDer.SkipOptionalField(body, 10);
        KerberosDer.SkipOptionalField(body, 11);
        body.ThrowIfNotEmpty();
        KerberosPrincipalName? clientName = clientNameField is { } encoded
            ? KerberosDer.ReadPrincipalNameField(new AsnReader(encoded, KerberosDer.Rules), 1, realm)
            : null;
        return new KerberosKdcRequest(isTicketGrantingRequest, preAuthenticationData, clientName, serverName, till, nonce, encryptionTypeNumbers);
    }

    // padata [3] SEQUENCE OF PA-DATA, PA-DATA ::= SEQUENCE { padata-type [1] Int32, padata-value [2] OCTET STRING }
    private static List<(int Type, byte[] Value)> ReadPreAuthenticationData(AsnReader fields)
    {
        AsnReader field = KerberosDer.ReadField(fields, 3);
        AsnReader sequence = field.ReadSequence();
        field.ThrowIfNotEmpty();
        List<(int Type, byte[] Value)> preAuthenticationData = [];
        while (sequence.HasData)
        {
            AsnReader paData = sequence.ReadSequence();
            int type = KerberosDer.ReadInt32Field(paData, 1);
            byte[] value = KerberosDer.ReadOctetStringField(paData, 2);
            paData.ThrowIfNotEmpty();
            preAuthenticationData.Add((type, value));
        }

        return preAuthenticationData;
    }

    private static List<int> ReadEncryptionTypes(AsnReader field)
    {
        AsnReader sequence = field.ReadSequence();
        field.ThrowIfNotEmpty();
        List<int> encryptionTypeNumbers = [];
        while (sequence.HasData)
        {
            if (!sequence.TryReadInt32(out int encryptionTypeNumber))
            {
                throw new AsnContentException("An enctype is out of the Int32 range.");
            }

            encryptionTypeNumbers.Add(encryptionTypeNumber);
        }

        return encryptionTypeNumbers;
    }
}
