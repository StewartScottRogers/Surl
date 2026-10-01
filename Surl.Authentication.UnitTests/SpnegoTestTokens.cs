using System.Formats.Asn1;

namespace Surl.Authentication;

/// <summary>
/// Builds the client's SPNEGO tokens (RFC 4178 section 4.2) around NTLM messages, for the
/// Negotiate tests: the pinned Windows reference build sent none on the lane machine
/// (Fixtures/README.md, <c>negotiate-no-token</c>), so the NTLM messages it sent for BL-120 are
/// wrapped as the RFC lays SPNEGO out.
/// </summary>
internal static class SpnegoTestTokens
{
    public const string KerberosOid = "1.2.840.113554.1.2.2";

    public const string MicrosoftKerberosOid = "1.2.840.48018.1.2.2";

    public const string NtlmOid = "1.3.6.1.4.1.311.2.2.10";

    /// <summary>
    /// An <c>InitialContextToken</c> carrying a <c>NegTokenInit</c>, with <c>reqFlags</c> and
    /// <c>mechListMIC</c> when asked for, as a client may send them.
    /// </summary>
    public static byte[] NegTokenInit(
        string[] mechTypes,
        byte[]? mechToken,
        bool withReqFlags = false,
        bool withMechListMic = false,
        byte[]? mechListMic = null)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence(new Asn1Tag(TagClass.Application, 0, isConstructed: true)))
        {
            writer.WriteObjectIdentifier("1.3.6.1.5.5.2");
            using (writer.PushSequence(Context(0)))
            using (writer.PushSequence())
            {
                using (writer.PushSequence(Context(0)))
                using (writer.PushSequence())
                {
                    foreach (var mechType in mechTypes)
                    {
                        writer.WriteObjectIdentifier(mechType);
                    }
                }

                if (withReqFlags)
                {
                    using var reqFlags = writer.PushSequence(Context(1));
                    writer.WriteBitString([0x00]);
                }

                WriteOctetStringField(writer, 2, mechToken);
                WriteOctetStringField(writer, 3, mechListMic ?? (withMechListMic ? [1, 2, 3] : null));
            }
        }

        return writer.Encode();
    }

    /// <summary>
    /// The DER of a <c>MechTypeList</c>, which a <c>mechListMIC</c> is taken over (RFC 4178 section 5).
    /// </summary>
    public static byte[] MechTypesDer(string[] mechTypes)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            foreach (var mechType in mechTypes)
            {
                writer.WriteObjectIdentifier(mechType);
            }
        }

        return writer.Encode();
    }

    /// <summary>
    /// A client's <c>negTokenResp</c>, with <c>negState</c>, <c>supportedMech</c> and
    /// <c>mechListMIC</c> when asked for.
    /// </summary>
    public static byte[] NegTokenResp(byte[]? responseToken, bool withEveryOtherField = false, byte[]? mechListMic = null)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence(Context(1)))
        using (writer.PushSequence())
        {
            if (withEveryOtherField)
            {
                using (writer.PushSequence(Context(0)))
                {
                    writer.WriteEnumeratedValue(SpnegoNegState.AcceptIncomplete);
                }

                using (writer.PushSequence(Context(1)))
                {
                    writer.WriteObjectIdentifier(NtlmOid);
                }
            }

            WriteOctetStringField(writer, 2, responseToken);
            WriteOctetStringField(writer, 3, mechListMic ?? (withEveryOtherField ? [1, 2, 3] : null));
        }

        return writer.Encode();
    }

    public static string Authorization(byte[] token) => $"Negotiate {Convert.ToBase64String(token)}";

    private static Asn1Tag Context(int number) => new(TagClass.ContextSpecific, number, isConstructed: true);

    private static void WriteOctetStringField(AsnWriter writer, int number, byte[]? value)
    {
        if (value is not null)
        {
            using var field = writer.PushSequence(Context(number));
            writer.WriteOctetString(value);
        }
    }
}
