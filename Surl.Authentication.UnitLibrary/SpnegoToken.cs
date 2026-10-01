using System.Formats.Asn1;

namespace Surl.Authentication;

/// <summary>
/// The SPNEGO tokens of RFC 4178 section 4.2 that HTTP Negotiate (RFC 4559) carries, read and
/// written as DER with <see cref="AsnReader"/> and <see cref="AsnWriter"/>: the client's first
/// token, a <c>NegTokenInit</c> inside RFC 2743 section 3.1's <c>InitialContextToken</c>
/// (<c>[APPLICATION 0]</c>, the SPNEGO object identifier, then <c>negTokenInit [0]</c>), and every
/// later token of either side, a bare <c>negTokenResp [1]</c>. A token that is not well-formed
/// DER of that shape reads as <see langword="null"/>, never as an exception.
/// </summary>
internal static class SpnegoToken
{
    /// <summary>
    /// SPNEGO's own object identifier, which the <c>InitialContextToken</c> names.
    /// </summary>
    public const string SpnegoOid = "1.3.6.1.5.5.2";

    /// <summary>
    /// NTLM's mechanism object identifier (NTLMSSP, [MS-SPNG] section 2.2).
    /// </summary>
    public const string NtlmOid = "1.3.6.1.4.1.311.2.2.10";

    /// <summary>
    /// The Kerberos V5 mechanism object identifier (RFC 1964).
    /// </summary>
    public const string KerberosOid = "1.2.840.113554.1.2.2";

    /// <summary>
    /// Microsoft's Kerberos object identifier, which Windows lists first ([MS-SPNG] section 3.3.5.1).
    /// </summary>
    public const string MicrosoftKerberosOid = "1.2.840.48018.1.2.2";

    private static readonly Asn1Tag InitialContextTokenTag = new(TagClass.Application, 0, isConstructed: true);

    private static readonly Asn1Tag NegTokenInitTag = new(TagClass.ContextSpecific, 0, isConstructed: true);

    private static readonly Asn1Tag NegTokenRespTag = new(TagClass.ContextSpecific, 1, isConstructed: true);

    /// <summary>
    /// Whether <paramref name="token"/> starts as an <c>InitialContextToken</c> does, so it is
    /// read with <see cref="ReadNegTokenInit"/>.
    /// </summary>
    /// <param name="token">The decoded token.</param>
    /// <returns><see langword="true"/> when its first byte is <c>[APPLICATION 0]</c>'s.</returns>
    public static bool IsInitialContextToken(ReadOnlySpan<byte> token) =>
        token.Length > 0 && token[0] == 0x60;

    /// <summary>
    /// Whether <paramref name="token"/> starts as a <c>negTokenResp</c> does, so it is read with
    /// <see cref="ReadNegTokenResp"/>.
    /// </summary>
    /// <param name="token">The decoded token.</param>
    /// <returns><see langword="true"/> when its first byte is <c>[1]</c>'s.</returns>
    public static bool IsNegTokenResp(ReadOnlySpan<byte> token) =>
        token.Length > 0 && token[0] == 0xA1;

    /// <summary>
    /// Reads the client's first token.
    /// </summary>
    /// <param name="token">The decoded <c>InitialContextToken</c>.</param>
    /// <returns>Its <c>NegTokenInit</c>, or <see langword="null"/> when it is not SPNEGO or not well-formed.</returns>
    public static SpnegoNegTokenInit? ReadNegTokenInit(byte[] token)
    {
        try
        {
            var outer = new AsnReader(token, AsnEncodingRules.DER);
            var initialContextToken = outer.ReadSequence(InitialContextTokenTag);
            outer.ThrowIfNotEmpty();
            if (initialContextToken.ReadObjectIdentifier() != SpnegoOid)
            {
                return null;
            }

            var negTokenInit = initialContextToken.ReadSequence(NegTokenInitTag);
            initialContextToken.ThrowIfNotEmpty();
            var fields = negTokenInit.ReadSequence();
            negTokenInit.ThrowIfNotEmpty();

            return ReadNegTokenInitFields(fields);
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a later client token.
    /// </summary>
    /// <param name="token">The decoded <c>negTokenResp</c>.</param>
    /// <returns>Its <c>responseToken</c> and <c>mechListMIC</c>, or <see langword="null"/> when it is not well-formed.</returns>
    public static SpnegoNegTokenResp? ReadNegTokenResp(byte[] token)
    {
        try
        {
            var outer = new AsnReader(token, AsnEncodingRules.DER);
            var negTokenResp = outer.ReadSequence(NegTokenRespTag);
            outer.ThrowIfNotEmpty();
            var fields = negTokenResp.ReadSequence();
            negTokenResp.ThrowIfNotEmpty();

            return ReadNegTokenRespFields(fields);
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the server's <c>negTokenResp</c> (RFC 4178 section 4.2.2).
    /// </summary>
    /// <param name="negState">The <c>negState</c>.</param>
    /// <param name="supportedMech">The <c>supportedMech</c>, sent in the first reply only; <see langword="null"/> to leave it out.</param>
    /// <param name="responseToken">The <c>responseToken</c>; <see langword="null"/> to leave it out.</param>
    /// <param name="mechListMic">The <c>mechListMIC</c>; <see langword="null"/> to leave it out.</param>
    /// <returns>The DER bytes.</returns>
    public static byte[] WriteNegTokenResp(
        SpnegoNegState negState, string? supportedMech, byte[]? responseToken, byte[]? mechListMic = null)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence(NegTokenRespTag))
        using (writer.PushSequence())
        {
            using (writer.PushSequence(ContextTag(0)))
            {
                writer.WriteEnumeratedValue(negState);
            }

            if (supportedMech is not null)
            {
                using var supportedMechField = writer.PushSequence(ContextTag(1));
                writer.WriteObjectIdentifier(supportedMech);
            }

            WriteOctetStringField(writer, 2, responseToken);
            WriteOctetStringField(writer, 3, mechListMic);
        }

        return writer.Encode();
    }

    private static void WriteOctetStringField(AsnWriter writer, int number, byte[]? value)
    {
        if (value is not null)
        {
            using var field = writer.PushSequence(ContextTag(number));
            writer.WriteOctetString(value);
        }
    }

    private static Asn1Tag ContextTag(int number) => new(TagClass.ContextSpecific, number, isConstructed: true);

    // NegTokenInit ::= SEQUENCE { mechTypes [0] MechTypeList, reqFlags [1] ContextFlags OPTIONAL,
    //   mechToken [2] OCTET STRING OPTIONAL, mechListMIC [3] OCTET STRING OPTIONAL }
    private static SpnegoNegTokenInit ReadNegTokenInitFields(AsnReader fields)
    {
        var mechTypesField = fields.ReadSequence(ContextTag(0));
        var mechTypesDer = mechTypesField.ReadEncodedValue().ToArray();
        mechTypesField.ThrowIfNotEmpty();
        var mechTypeList = new AsnReader(mechTypesDer, AsnEncodingRules.DER).ReadSequence();
        var mechTypes = new List<string>();
        while (mechTypeList.HasData)
        {
            mechTypes.Add(mechTypeList.ReadObjectIdentifier());
        }

        SkipField(fields, 1);
        var mechToken = ReadOctetStringField(fields, 2);
        var mechListMic = ReadOctetStringField(fields, 3);
        fields.ThrowIfNotEmpty();

        return new SpnegoNegTokenInit(mechTypes, mechToken, mechTypesDer, mechListMic);
    }

    // NegTokenResp ::= SEQUENCE { negState [0] ENUMERATED OPTIONAL, supportedMech [1] MechType
    //   OPTIONAL, responseToken [2] OCTET STRING OPTIONAL, mechListMIC [3] OCTET STRING OPTIONAL }
    private static SpnegoNegTokenResp ReadNegTokenRespFields(AsnReader fields)
    {
        SkipField(fields, 0);
        SkipField(fields, 1);
        var responseToken = ReadOctetStringField(fields, 2);
        var mechListMic = ReadOctetStringField(fields, 3);
        fields.ThrowIfNotEmpty();

        return new SpnegoNegTokenResp(responseToken ?? [], mechListMic);
    }

    private static void SkipField(AsnReader fields, int number)
    {
        if (fields.HasData && fields.PeekTag() == ContextTag(number))
        {
            fields.ReadEncodedValue();
        }
    }

    private static byte[]? ReadOctetStringField(AsnReader fields, int number)
    {
        if (!fields.HasData || fields.PeekTag() != ContextTag(number))
        {
            return null;
        }

        var field = fields.ReadSequence(ContextTag(number));
        var value = field.ReadOctetString();
        field.ThrowIfNotEmpty();

        return value;
    }
}
