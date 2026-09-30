using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Text;

namespace Surl.Kerberos.TestKdc;

/// <summary>
/// Writes the DER building blocks of RFC 4120 section 5 with <see cref="AsnWriter" />, the
/// counterpart of <see cref="KerberosDer" />: the explicitly tagged <c>[n]</c> fields of a
/// <c>SEQUENCE</c>, <c>KerberosString</c> (a <c>GeneralString</c>, which <see cref="AsnWriter" />
/// does not write as text), <c>KerberosTime</c>, <c>KerberosFlags</c>, <c>PrincipalName</c>,
/// <c>EncryptionKey</c>, <c>EncryptedData</c> and <c>PA-DATA</c>.
/// </summary>
internal static class KerberosDerWriter
{
    /// <summary>RFC 4120 section 6.2's <c>NT-PRINCIPAL</c>, the name type of a user.</summary>
    public const int PrincipalNameType = 1;

    /// <summary>RFC 4120 section 6.2's <c>NT-SRV-INST</c>, the name type of a service and of <c>krbtgt</c>.</summary>
    public const int ServiceInstanceNameType = 2;

    /// <summary>Makes a writer under the rules every Kerberos message is written under.</summary>
    /// <returns>An empty DER writer.</returns>
    public static AsnWriter NewWriter() => new(KerberosDer.Rules);

    /// <summary>Opens the field <c>[n]</c>; dispose the scope to close it.</summary>
    /// <param name="writer">The writer inside the enclosing <c>SEQUENCE</c>.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The scope of the field.</returns>
    public static AsnWriter.Scope PushField(AsnWriter writer, int number) => writer.PushSequence(KerberosDer.ContextTag(number));

    /// <summary>Writes the field <c>[n] Int32</c>, or <c>UInt32</c> when <paramref name="value" /> is one.</summary>
    /// <param name="writer">The writer inside the enclosing <c>SEQUENCE</c>.</param>
    /// <param name="number">The field's tag number.</param>
    /// <param name="value">The integer.</param>
    public static void WriteIntegerField(AsnWriter writer, int number, long value)
    {
        using AsnWriter.Scope field = PushField(writer, number);
        writer.WriteInteger(value);
    }

    /// <summary>Writes the field <c>[n] OCTET STRING</c>.</summary>
    /// <param name="writer">The writer inside the enclosing <c>SEQUENCE</c>.</param>
    /// <param name="number">The field's tag number.</param>
    /// <param name="value">The octets.</param>
    public static void WriteOctetStringField(AsnWriter writer, int number, ReadOnlySpan<byte> value)
    {
        using AsnWriter.Scope field = PushField(writer, number);
        writer.WriteOctetString(value);
    }

    /// <summary>Writes the field <c>[n] KerberosString</c>.</summary>
    /// <param name="writer">The writer inside the enclosing <c>SEQUENCE</c>.</param>
    /// <param name="number">The field's tag number.</param>
    /// <param name="value">The string, encoded as UTF-8.</param>
    public static void WriteStringField(AsnWriter writer, int number, string value)
    {
        using AsnWriter.Scope field = PushField(writer, number);
        WriteGeneralString(writer, value);
    }

    /// <summary>Writes a <c>GeneralString</c>: tag <c>0x1B</c>, the DER length, then the UTF-8 bytes.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="value">The string.</param>
    public static void WriteGeneralString(AsnWriter writer, string value)
    {
        byte[] content = Encoding.UTF8.GetBytes(value);
        writer.WriteEncodedValue([0x1B, .. EncodeLength(content.Length), .. content]);
    }

    /// <summary>Writes the field <c>[n] KerberosTime</c>, a <c>GeneralizedTime</c> in whole seconds.</summary>
    /// <param name="writer">The writer inside the enclosing <c>SEQUENCE</c>.</param>
    /// <param name="number">The field's tag number.</param>
    /// <param name="value">The time; anything below a second is dropped.</param>
    public static void WriteTimeField(AsnWriter writer, int number, DateTimeOffset value)
    {
        using AsnWriter.Scope field = PushField(writer, number);
        writer.WriteGeneralizedTime(WholeSeconds(value), omitFractionalSeconds: true);
    }

    /// <summary>Drops everything below a second from <paramref name="value" />, in UTC.</summary>
    /// <param name="value">The time.</param>
    /// <returns>The whole-second UTC time a <c>KerberosTime</c> carries.</returns>
    public static DateTimeOffset WholeSeconds(DateTimeOffset value) =>
        new(value.UtcTicks - (value.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

    /// <summary>Writes the field <c>[n] KerberosFlags</c>: 32 bits, bit 0 the most significant.</summary>
    /// <param name="writer">The writer inside the enclosing <c>SEQUENCE</c>.</param>
    /// <param name="number">The field's tag number.</param>
    /// <param name="flags">The flags, bit 0 as <c>0x80000000</c>.</param>
    public static void WriteFlagsField(AsnWriter writer, int number, uint flags)
    {
        Span<byte> bits = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bits, flags);
        using AsnWriter.Scope field = PushField(writer, number);
        writer.WriteBitString(bits);
    }

    /// <summary>Gets the <c>KerberosFlags</c> value with flag <paramref name="bit" /> set.</summary>
    /// <param name="bit">The flag's bit number, 0 the most significant.</param>
    /// <returns>The flag as a 32-bit value.</returns>
    public static uint Flag(int bit) => 0x80000000u >> bit;

    /// <summary>
    /// Writes the field <c>[n] PrincipalName</c>: <c>NT-PRINCIPAL</c> for one component,
    /// <c>NT-SRV-INST</c> for more.
    /// </summary>
    /// <param name="writer">The writer inside the enclosing <c>SEQUENCE</c>.</param>
    /// <param name="number">The field's tag number.</param>
    /// <param name="name">The principal; its realm is written elsewhere.</param>
    public static void WritePrincipalNameField(AsnWriter writer, int number, KerberosPrincipalName name)
    {
        using AsnWriter.Scope field = PushField(writer, number);
        using AsnWriter.Scope principalName = writer.PushSequence();
        WriteIntegerField(writer, 0, name.Components.Count == 1 ? PrincipalNameType : ServiceInstanceNameType);
        using AsnWriter.Scope nameStringField = PushField(writer, 1);
        using AsnWriter.Scope nameStrings = writer.PushSequence();
        foreach (string component in name.Components)
        {
            WriteGeneralString(writer, component);
        }
    }

    /// <summary>Writes the field <c>[n] EncryptionKey</c>.</summary>
    /// <param name="writer">The writer inside the enclosing <c>SEQUENCE</c>.</param>
    /// <param name="number">The field's tag number.</param>
    /// <param name="encryptionType">The key's type.</param>
    /// <param name="key">The key.</param>
    public static void WriteEncryptionKeyField(AsnWriter writer, int number, KerberosEncryptionType encryptionType, ReadOnlySpan<byte> key)
    {
        using AsnWriter.Scope field = PushField(writer, number);
        using AsnWriter.Scope encryptionKey = writer.PushSequence();
        WriteIntegerField(writer, 0, (int)encryptionType);
        WriteOctetStringField(writer, 1, key);
    }

    /// <summary>Writes the field <c>[n] EncryptedData</c>.</summary>
    /// <param name="writer">The writer inside the enclosing <c>SEQUENCE</c>.</param>
    /// <param name="number">The field's tag number.</param>
    /// <param name="encryptionType">The <c>etype</c>.</param>
    /// <param name="keyVersionNumber">The <c>kvno</c>, or <see langword="null" /> to leave it out.</param>
    /// <param name="cipherText">The <c>cipher</c>.</param>
    public static void WriteEncryptedDataField(AsnWriter writer, int number, KerberosEncryptionType encryptionType, uint? keyVersionNumber, ReadOnlySpan<byte> cipherText)
    {
        using AsnWriter.Scope field = PushField(writer, number);
        using AsnWriter.Scope encryptedData = writer.PushSequence();
        WriteIntegerField(writer, 0, (int)encryptionType);
        if (keyVersionNumber is uint version)
        {
            WriteIntegerField(writer, 1, version);
        }

        WriteOctetStringField(writer, 2, cipherText);
    }

    /// <summary>Writes one <c>PA-DATA ::= SEQUENCE { padata-type [1] Int32, padata-value [2] OCTET STRING }</c>.</summary>
    /// <param name="writer">The writer inside the <c>SEQUENCE OF PA-DATA</c>.</param>
    /// <param name="type">The <c>padata-type</c>.</param>
    /// <param name="value">The <c>padata-value</c>.</param>
    public static void WritePreAuthenticationData(AsnWriter writer, int type, ReadOnlySpan<byte> value)
    {
        using AsnWriter.Scope paData = writer.PushSequence();
        WriteIntegerField(writer, 1, type);
        WriteOctetStringField(writer, 2, value);
    }

    private static byte[] EncodeLength(int length)
    {
        if (length < 0x80)
        {
            return [(byte)length];
        }

        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, length);
        int skip = Array.FindIndex(bytes, value => value != 0);
        return [(byte)(0x80 | (bytes.Length - skip)), .. bytes[skip..]];
    }
}
