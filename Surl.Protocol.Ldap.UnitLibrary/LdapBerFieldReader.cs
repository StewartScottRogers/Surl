using System.Formats.Asn1;
using System.Numerics;
using System.Text;

namespace Surl.Protocol.Ldap;

/// <summary>
/// Reads the fields of RFC 4511's ASN.1 from an <see cref="AsnReader"/> with BER rules, turning
/// every way a field can be malformed into an <see cref="LdapDecodeException"/> that names it.
/// </summary>
/// <remarks>
/// Every read first peeks the next element, which checks its tag and length and refuses the
/// indefinite length form, then checks its tag against the one the ASN.1 expects in that place.
/// Only then is the value read, so a failure of <see cref="AsnReader"/> past that point is a
/// value that is not valid for its type.
/// </remarks>
internal static class LdapBerFieldReader
{
    private const byte IndefiniteLengthOctet = 0x80;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Peeks the next element's tag, checking its tag and length.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The tag.</returns>
    /// <exception cref="LdapDecodeException">No element is left, or its tag or length is malformed or indefinite.</exception>
    public static Asn1Tag PeekTag(AsnReader reader)
    {
        if (!reader.HasData)
        {
            throw new LdapDecodeException(LdapDecodeOutcome.MissingElement);
        }

        var (tag, encoded) = Reading(() => (reader.PeekTag(), reader.PeekEncodedValue()), LdapDecodeOutcome.MalformedTagOrLength);
        return encoded.Span[tag.CalculateEncodedSize()] == IndefiniteLengthOctet
            ? throw new LdapDecodeException(LdapDecodeOutcome.IndefiniteLength)
            : tag;
    }

    /// <summary>
    /// Whether an element is left and has <paramref name="tag"/>'s class and number.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="tag">The tag an optional element would have.</param>
    /// <returns><see langword="true"/> when the next element is that one.</returns>
    public static bool IsNext(AsnReader reader, Asn1Tag tag) => reader.HasData && PeekTag(reader).HasSameClassAndValue(tag);

    /// <summary>
    /// Reads a constructed element with <paramref name="tag"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="tag">The tag expected.</param>
    /// <returns>A reader over the element's contents.</returns>
    public static AsnReader ReadSequence(AsnReader reader, Asn1Tag tag)
    {
        Expect(reader, tag);
        return Reading(() => reader.ReadSequence(tag), LdapDecodeOutcome.InvalidValue);
    }

    /// <summary>
    /// Reads an <c>INTEGER</c> with <paramref name="tag"/> that must lie in a range.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="tag">The tag expected.</param>
    /// <param name="minimum">The smallest value allowed.</param>
    /// <param name="maximum">The largest value allowed.</param>
    /// <returns>The value.</returns>
    public static int ReadInt32(AsnReader reader, Asn1Tag tag, int minimum, int maximum = int.MaxValue)
    {
        Expect(reader, tag);
        var value = Reading(() => reader.TryReadInt32(out var read, tag) ? read : (int?)null, LdapDecodeOutcome.InvalidValue);
        return value is { } inRange && inRange >= minimum && inRange <= maximum
            ? inRange
            : throw new LdapDecodeException(LdapDecodeOutcome.InvalidValue);
    }

    /// <summary>
    /// Reads an <c>ENUMERATED</c> whose values run from 0 to <paramref name="maximum"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="maximum">The largest value the enumeration lists.</param>
    /// <returns>The value.</returns>
    public static int ReadEnumerated(AsnReader reader, int maximum)
    {
        Expect(reader, Asn1Tag.Enumerated);
        var bytes = Reading(() => reader.ReadEnumeratedBytes(), LdapDecodeOutcome.InvalidValue);
        var value = new BigInteger(bytes.Span, isUnsigned: false, isBigEndian: true);
        return value >= 0 && value <= maximum
            ? (int)value
            : throw new LdapDecodeException(LdapDecodeOutcome.EnumerationOutOfRange);
    }

    /// <summary>
    /// Reads an <c>OCTET STRING</c> with <paramref name="tag"/>, in either BER form.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="tag">The tag expected.</param>
    /// <returns>The bytes.</returns>
    public static byte[] ReadOctetString(AsnReader reader, Asn1Tag tag)
    {
        Expect(reader, tag);
        return Reading(() => reader.ReadOctetString(tag), LdapDecodeOutcome.InvalidValue);
    }

    /// <summary>
    /// Reads an <c>LDAPString</c> (or an <c>LDAPOID</c>) with <paramref name="tag"/>: an
    /// <c>OCTET STRING</c> holding UTF-8.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="tag">The tag expected.</param>
    /// <returns>The text.</returns>
    public static string ReadString(AsnReader reader, Asn1Tag tag)
    {
        var bytes = ReadOctetString(reader, tag);
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new LdapDecodeException(LdapDecodeOutcome.InvalidValue);
        }
    }

    /// <summary>
    /// Reads a <c>BOOLEAN</c> with <paramref name="tag"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="tag">The tag expected.</param>
    /// <returns>The value.</returns>
    public static bool ReadBoolean(AsnReader reader, Asn1Tag tag)
    {
        Expect(reader, tag);
        return Reading(() => reader.ReadBoolean(tag), LdapDecodeOutcome.InvalidValue);
    }

    /// <summary>
    /// Reads a <c>NULL</c> with <paramref name="tag"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="tag">The tag expected.</param>
    public static void ReadNull(AsnReader reader, Asn1Tag tag)
    {
        Expect(reader, tag);
        Reading(() => { reader.ReadNull(tag); return true; }, LdapDecodeOutcome.InvalidValue);
    }

    /// <summary>
    /// Skips the next element, whatever it is, once its tag and length have been checked.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The element's tag.</returns>
    public static Asn1Tag Skip(AsnReader reader)
    {
        var tag = PeekTag(reader);
        reader.ReadEncodedValue();
        return tag;
    }

    /// <summary>
    /// Checks that nothing is left after the last element of a sequence or of the message.
    /// </summary>
    /// <param name="reader">The reader over the sequence.</param>
    public static void EnsureEnd(AsnReader reader)
    {
        if (reader.HasData)
        {
            throw new LdapDecodeException(LdapDecodeOutcome.TrailingBytes);
        }
    }

    private static void Expect(AsnReader reader, Asn1Tag tag)
    {
        if (!PeekTag(reader).HasSameClassAndValue(tag))
        {
            throw new LdapDecodeException(LdapDecodeOutcome.UnexpectedTag);
        }
    }

    private static T Reading<T>(Func<T> read, LdapDecodeOutcome outcome)
    {
        try
        {
            return read();
        }
        catch (AsnContentException)
        {
            throw new LdapDecodeException(outcome);
        }
    }
}
