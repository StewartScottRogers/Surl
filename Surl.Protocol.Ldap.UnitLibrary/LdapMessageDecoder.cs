using System.Formats.Asn1;
using static Surl.Protocol.Ldap.LdapBerFieldReader;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Decodes one <c>LDAPMessage</c> a client sent (RFC 4511, section 4 and appendix B) from its
/// BER encoding into an <see cref="LdapMessage"/>, or names what is malformed.
/// </summary>
/// <remarks>
/// It decodes <c>BindRequest</c>, <c>UnbindRequest</c>, <c>SearchRequest</c>,
/// <c>AbandonRequest</c> and <c>ExtendedRequest</c>, with their <c>controls</c>; every other
/// operation is reported by its tag as an <see cref="LdapUnrecognizedOperation"/>. The
/// <c>messageID</c> is carried by a malformed result whenever it could be read, so the server can
/// answer <see cref="LdapResultCode.ProtocolError"/> to it.
/// </remarks>
internal sealed class LdapMessageDecoder
{
    private const int MaxBindVersion = 127;

    private readonly LdapFilterDecoder filterDecoder;
    private int? messageId;

    private LdapMessageDecoder(int maxFilterDepth) => filterDecoder = new LdapFilterDecoder(maxFilterDepth);

    /// <summary>
    /// Decodes one message.
    /// </summary>
    /// <param name="message">The message's whole encoding, as <see cref="LdapMessageFrameReader"/> reads it.</param>
    /// <param name="maxFilterDepth">
    /// How deep a search filter may nest: a filter with no <c>and</c>, <c>or</c> or <c>not</c> is
    /// depth 1, and each of those adds one to the filters inside it. At least 1.
    /// </param>
    /// <returns>The message, or what is malformed and the message ID when it could be read.</returns>
    public static LdapDecodeResult Decode(ReadOnlyMemory<byte> message, int maxFilterDepth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFilterDepth, 1);

        var decoder = new LdapMessageDecoder(maxFilterDepth);
        try
        {
            return LdapDecodeResult.Decoded(decoder.DecodeMessage(message));
        }
        catch (LdapDecodeException refused)
        {
            return LdapDecodeResult.Malformed(refused.Outcome, decoder.messageId);
        }
    }

    private static LdapProtocolOperation ReadBindRequest(AsnReader reader)
    {
        var bind = ReadSequence(reader, LdapTags.BindRequest);
        var version = ReadInt32(bind, Asn1Tag.Integer, 1, MaxBindVersion);
        var name = ReadString(bind, Asn1Tag.PrimitiveOctetString);
        var authentication = ReadAuthentication(bind);
        EnsureEnd(bind);

        return new LdapBindRequest(version, name, authentication);
    }

    private static LdapBindAuthentication ReadAuthentication(AsnReader reader)
    {
        var tag = PeekTag(reader);
        if (tag.HasSameClassAndValue(LdapTags.Context(0)))
        {
            return new LdapSimpleAuthentication(ReadOctetString(reader, LdapTags.Context(0)));
        }

        if (tag.HasSameClassAndValue(LdapTags.Context(3)))
        {
            return ReadSaslAuthentication(reader);
        }

        return IsSicilyChoice(tag)
            ? new LdapSicilyAuthentication((LdapSicilyChoice)tag.TagValue, ReadOctetString(reader, tag))
            : new LdapUnsupportedAuthentication(Skip(reader));
    }

    // Sicily's [9], [10] and [11] (MS-ADTS section 5.1.1.1.3), each an OCTET STRING.
    private static bool IsSicilyChoice(Asn1Tag tag) =>
        tag.TagClass == TagClass.ContextSpecific && Enum.IsDefined((LdapSicilyChoice)tag.TagValue);

    private static LdapSaslAuthentication ReadSaslAuthentication(AsnReader reader)
    {
        var sasl = ReadSequence(reader, LdapTags.Context(3, isConstructed: true));
        var mechanism = ReadString(sasl, Asn1Tag.PrimitiveOctetString);
        var credentials = sasl.HasData ? ReadOctetString(sasl, Asn1Tag.PrimitiveOctetString) : null;
        EnsureEnd(sasl);

        return new LdapSaslAuthentication(mechanism, credentials);
    }

    private static LdapProtocolOperation ReadUnbindRequest(AsnReader reader)
    {
        ReadNull(reader, LdapTags.UnbindRequest);

        return new LdapUnbindRequest();
    }

    private static LdapProtocolOperation ReadCompareRequest(AsnReader reader)
    {
        var compare = ReadSequence(reader, LdapTags.CompareRequest);
        var entry = ReadString(compare, Asn1Tag.PrimitiveOctetString);
        var assertion = ReadSequence(compare, Asn1Tag.Sequence);
        var description = ReadString(assertion, Asn1Tag.PrimitiveOctetString);
        var value = ReadOctetString(assertion, Asn1Tag.PrimitiveOctetString);
        EnsureEnd(assertion);
        EnsureEnd(compare);

        return new LdapCompareRequest(entry, description, value);
    }

    private static LdapProtocolOperation ReadAbandonRequest(AsnReader reader) =>
        new LdapAbandonRequest(ReadInt32(reader, LdapTags.AbandonRequest, 0));

    private static LdapProtocolOperation ReadExtendedRequest(AsnReader reader)
    {
        var extended = ReadSequence(reader, LdapTags.ExtendedRequest);
        var name = ReadString(extended, LdapTags.Context(0));
        var value = extended.HasData ? ReadOctetString(extended, LdapTags.Context(1)) : null;
        EnsureEnd(extended);

        return new LdapExtendedRequest(name, value);
    }

    private static List<LdapControl> ReadControls(AsnReader reader)
    {
        var controls = new List<LdapControl>();
        if (!IsNext(reader, LdapTags.Controls))
        {
            return controls;
        }

        var sequence = ReadSequence(reader, LdapTags.Controls);
        while (sequence.HasData)
        {
            controls.Add(ReadControl(sequence));
        }

        return controls;
    }

    private static LdapControl ReadControl(AsnReader reader)
    {
        var control = ReadSequence(reader, Asn1Tag.Sequence);
        var type = ReadString(control, Asn1Tag.PrimitiveOctetString);
        var criticality = IsNext(control, Asn1Tag.Boolean) && ReadBoolean(control, Asn1Tag.Boolean);
        var value = control.HasData ? ReadOctetString(control, Asn1Tag.PrimitiveOctetString) : null;
        EnsureEnd(control);

        return new LdapControl(type, criticality, value);
    }

    private LdapMessage DecodeMessage(ReadOnlyMemory<byte> encoded)
    {
        var outer = new AsnReader(encoded, AsnEncodingRules.BER);
        var message = ReadSequence(outer, Asn1Tag.Sequence);
        messageId = ReadInt32(message, Asn1Tag.Integer, 0);
        var operation = ReadOperation(message);
        var controls = ReadControls(message);
        EnsureEnd(message);
        EnsureEnd(outer);

        return new LdapMessage(messageId.Value, operation, controls);
    }

    private LdapProtocolOperation ReadOperation(AsnReader reader)
    {
        return ApplicationTagNumber(PeekTag(reader)) switch
        {
            0 => ReadBindRequest(reader),
            2 => ReadUnbindRequest(reader),
            3 => ReadSearchRequest(reader),
            var number => ReadOtherOperation(reader, number),
        };
    }

    // The requests no curl build sends, and every unrecognized one.
    private static LdapProtocolOperation ReadOtherOperation(AsnReader reader, int applicationTagNumber) => applicationTagNumber switch
    {
        14 => ReadCompareRequest(reader),
        16 => ReadAbandonRequest(reader),
        23 => ReadExtendedRequest(reader),
        _ => new LdapUnrecognizedOperation(Skip(reader)),
    };

    // The tag's number when it is an application tag, as every request's is; -1 otherwise.
    private static int ApplicationTagNumber(Asn1Tag tag) => tag.TagClass == TagClass.Application ? tag.TagValue : -1;

    private LdapProtocolOperation ReadSearchRequest(AsnReader reader)
    {
        var search = ReadSequence(reader, LdapTags.SearchRequest);
        var baseObject = ReadString(search, Asn1Tag.PrimitiveOctetString);
        var scope = (LdapSearchScope)ReadEnumerated(search, (int)LdapSearchScope.WholeSubtree);
        var derefAliases = (LdapDerefAliases)ReadEnumerated(search, (int)LdapDerefAliases.DerefAlways);
        var sizeLimit = ReadInt32(search, Asn1Tag.Integer, 0);
        var timeLimit = ReadInt32(search, Asn1Tag.Integer, 0);
        var typesOnly = ReadBoolean(search, Asn1Tag.Boolean);
        var filter = filterDecoder.ReadFilter(search);
        var attributes = ReadAttributeSelection(search);
        EnsureEnd(search);

        return new LdapSearchRequest(baseObject, scope, derefAliases, sizeLimit, timeLimit, typesOnly, filter, attributes);
    }

    private static List<string> ReadAttributeSelection(AsnReader reader)
    {
        var selection = ReadSequence(reader, Asn1Tag.Sequence);
        var attributes = new List<string>();
        while (selection.HasData)
        {
            attributes.Add(ReadString(selection, Asn1Tag.PrimitiveOctetString));
        }

        return attributes;
    }
}
