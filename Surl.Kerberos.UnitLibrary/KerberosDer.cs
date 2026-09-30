using System.Formats.Asn1;
using System.Text;

namespace Surl.Kerberos;

/// <summary>
/// Reads the DER building blocks of RFC 4120 section 5 with <see cref="AsnReader" />: the
/// <c>[APPLICATION n]</c> wrappers, the explicitly tagged <c>[n]</c> fields of a <c>SEQUENCE</c>,
/// <c>KerberosString</c> (a <c>GeneralString</c>, which <see cref="AsnReader" /> does not decode
/// as text), <c>KerberosTime</c>, <c>PrincipalName</c> and the flag bits of a
/// <c>KerberosFlags</c>. Anything malformed, out of range or left over throws
/// <see cref="AsnContentException" />, which <see cref="KerberosAcceptor" /> turns into the
/// refusal <c>malformed token</c>.
/// </summary>
internal static class KerberosDer
{
    /// <summary>The encoding rules every Kerberos message is read and written under.</summary>
    public const AsnEncodingRules Rules = AsnEncodingRules.DER;

    private static readonly Asn1Tag GeneralStringTag = new(UniversalTagNumber.GeneralString);

    /// <summary>Gets the tag of the explicitly tagged field <c>[n]</c>.</summary>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The constructed context-specific tag.</returns>
    public static Asn1Tag ContextTag(int number) => new(TagClass.ContextSpecific, number, isConstructed: true);

    /// <summary>Gets the tag of the message wrapper <c>[APPLICATION n]</c>.</summary>
    /// <param name="number">The message's application tag number.</param>
    /// <returns>The constructed application tag.</returns>
    public static Asn1Tag ApplicationTag(int number) => new(TagClass.Application, number, isConstructed: true);

    /// <summary>Reads bytes that are exactly one <c>[APPLICATION n] SEQUENCE</c>.</summary>
    /// <param name="bytes">The encoded message.</param>
    /// <param name="number">The message's application tag number.</param>
    /// <returns>A reader over the <c>SEQUENCE</c>'s fields.</returns>
    public static AsnReader ReadApplicationSequence(ReadOnlyMemory<byte> bytes, int number) =>
        ReadApplicationSequence(new AsnReader(bytes, Rules), number);

    /// <summary>Reads an <c>[APPLICATION n] SEQUENCE</c> that must be all <paramref name="container" /> holds.</summary>
    /// <param name="container">The reader the message is the only value of.</param>
    /// <param name="number">The message's application tag number.</param>
    /// <returns>A reader over the <c>SEQUENCE</c>'s fields.</returns>
    public static AsnReader ReadApplicationSequence(AsnReader container, int number)
    {
        AsnReader application = container.ReadSequence(ApplicationTag(number));
        container.ThrowIfNotEmpty();
        AsnReader fields = application.ReadSequence();
        application.ThrowIfNotEmpty();
        return fields;
    }

    /// <summary>Gets whether the next field of <paramref name="fields" /> is <c>[n]</c>.</summary>
    /// <param name="fields">The <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns><see langword="true" /> when the optional field is present.</returns>
    public static bool HasField(AsnReader fields, int number) => fields.HasData && fields.PeekTag() == ContextTag(number);

    /// <summary>Reads the required field <c>[n]</c>.</summary>
    /// <param name="fields">The <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns>A reader over the field's value.</returns>
    public static AsnReader ReadField(AsnReader fields, int number) => fields.ReadSequence(ContextTag(number));

    /// <summary>Skips the optional field <c>[n]</c> when it is present.</summary>
    /// <param name="fields">The <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    public static void SkipOptionalField(AsnReader fields, int number)
    {
        if (HasField(fields, number))
        {
            fields.ReadEncodedValue();
        }
    }

    /// <summary>Reads the field <c>[n] Int32</c>.</summary>
    /// <param name="fields">The <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The integer.</returns>
    public static int ReadInt32Field(AsnReader fields, int number)
    {
        AsnReader field = ReadField(fields, number);
        if (!field.TryReadInt32(out int value))
        {
            throw new AsnContentException("The integer is out of the Int32 range.");
        }

        field.ThrowIfNotEmpty();
        return value;
    }

    /// <summary>
    /// Reads the optional field <c>[n] UInt32</c>. A negative 32-bit value is taken as its two's
    /// complement, as MIT krb5 takes the sequence numbers older Windows clients encode that way.
    /// </summary>
    /// <param name="fields">The <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The integer, or <see langword="null" /> when the field is absent.</returns>
    public static uint? ReadOptionalUInt32Field(AsnReader fields, int number)
    {
        if (!HasField(fields, number))
        {
            return null;
        }

        AsnReader field = ReadField(fields, number);
        if (!field.TryReadInt64(out long value) || value is < int.MinValue or > uint.MaxValue)
        {
            throw new AsnContentException("The integer is out of the UInt32 range.");
        }

        field.ThrowIfNotEmpty();
        return unchecked((uint)value);
    }

    /// <summary>Reads the field <c>[n] OCTET STRING</c>.</summary>
    /// <param name="fields">The <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The octets.</returns>
    public static byte[] ReadOctetStringField(AsnReader fields, int number)
    {
        AsnReader field = ReadField(fields, number);
        byte[] value = field.ReadOctetString();
        field.ThrowIfNotEmpty();
        return value;
    }

    /// <summary>Reads the field <c>[n] KerberosString</c>.</summary>
    /// <param name="fields">The <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The string, decoded as UTF-8.</returns>
    public static string ReadStringField(AsnReader fields, int number)
    {
        AsnReader field = ReadField(fields, number);
        string value = ReadGeneralString(field);
        field.ThrowIfNotEmpty();
        return value;
    }

    /// <summary>Reads the field <c>[n] KerberosTime</c>, a <c>GeneralizedTime</c>.</summary>
    /// <param name="fields">The <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The time.</returns>
    public static DateTimeOffset ReadTimeField(AsnReader fields, int number)
    {
        AsnReader field = ReadField(fields, number);
        DateTimeOffset value = field.ReadGeneralizedTime();
        field.ThrowIfNotEmpty();
        return value;
    }

    /// <summary>Reads the optional field <c>[n] KerberosTime</c>.</summary>
    /// <param name="fields">The <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The time, or <see langword="null" /> when the field is absent.</returns>
    public static DateTimeOffset? ReadOptionalTimeField(AsnReader fields, int number) =>
        HasField(fields, number) ? ReadTimeField(fields, number) : null;

    /// <summary>Reads the field <c>[n] KerberosFlags</c>, a <c>BIT STRING</c>.</summary>
    /// <param name="fields">The <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The flag bits, bit 0 the most significant bit of the first byte.</returns>
    public static byte[] ReadFlagsField(AsnReader fields, int number)
    {
        AsnReader field = ReadField(fields, number);
        byte[] value = field.ReadBitString(out _);
        field.ThrowIfNotEmpty();
        return value;
    }

    /// <summary>Gets whether flag <paramref name="bit" /> of a <c>KerberosFlags</c> is set.</summary>
    /// <param name="flags">The flag bits.</param>
    /// <param name="bit">The flag's bit number, 0 the most significant bit of the first byte.</param>
    /// <returns><see langword="true" /> when the bit is present and set.</returns>
    public static bool IsFlagSet(byte[] flags, int bit) =>
        bit / 8 < flags.Length && (flags[bit / 8] & (0x80 >> (bit % 8))) != 0;

    /// <summary>
    /// Reads the field <c>[n] PrincipalName</c>: its name type, which surl does not use, and its
    /// one or more name components.
    /// </summary>
    /// <param name="fields">The <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <param name="realm">The realm the message names beside the principal.</param>
    /// <returns>The principal.</returns>
    public static KerberosPrincipalName ReadPrincipalNameField(AsnReader fields, int number, string realm)
    {
        AsnReader field = ReadField(fields, number);
        AsnReader principalName = field.ReadSequence();
        field.ThrowIfNotEmpty();
        ReadInt32Field(principalName, 0);
        AsnReader nameStringField = ReadField(principalName, 1);
        principalName.ThrowIfNotEmpty();
        AsnReader nameStrings = nameStringField.ReadSequence();
        nameStringField.ThrowIfNotEmpty();
        List<string> components = [];
        while (nameStrings.HasData)
        {
            components.Add(ReadGeneralString(nameStrings));
        }

        if (components.Count == 0)
        {
            throw new AsnContentException("A principal name has no components.");
        }

        return new KerberosPrincipalName(realm, components);
    }

    private static string ReadGeneralString(AsnReader reader)
    {
        if (reader.PeekTag() != GeneralStringTag)
        {
            throw new AsnContentException("A KerberosString is not a GeneralString.");
        }

        ReadOnlyMemory<byte> encoded = reader.ReadEncodedValue();
        AsnDecoder.ReadEncodedValue(encoded.Span, Rules, out int contentOffset, out int contentLength, out _);
        return Encoding.UTF8.GetString(encoded.Span.Slice(contentOffset, contentLength));
    }
}
