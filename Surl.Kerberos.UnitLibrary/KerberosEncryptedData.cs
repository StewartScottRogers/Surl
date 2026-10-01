using System.Formats.Asn1;

namespace Surl.Kerberos;

/// <summary>
/// RFC 4120 section 5.2.9's <c>EncryptedData</c>: the encryption type, the key version number
/// when the key has one, and the cipher text.
/// </summary>
/// <param name="EncryptionTypeNumber">The <c>etype</c>, as the IANA registry numbers it.</param>
/// <param name="KeyVersionNumber">The <c>kvno</c>, or <see langword="null" /> when absent.</param>
/// <param name="CipherText">The <c>cipher</c>.</param>
internal sealed record KerberosEncryptedData(int EncryptionTypeNumber, uint? KeyVersionNumber, byte[] CipherText)
{
    /// <summary>Reads the field <c>[n] EncryptedData</c>.</summary>
    /// <param name="fields">The enclosing <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The encrypted data.</returns>
    public static KerberosEncryptedData ReadField(AsnReader fields, int number)
    {
        AsnReader field = KerberosDer.ReadField(fields, number);
        AsnReader encryptedData = field.ReadSequence();
        field.ThrowIfNotEmpty();
        int encryptionTypeNumber = KerberosDer.ReadInt32Field(encryptedData, 0);
        uint? keyVersionNumber = KerberosDer.ReadOptionalUInt32Field(encryptedData, 1);
        byte[] cipherText = KerberosDer.ReadOctetStringField(encryptedData, 2);
        encryptedData.ThrowIfNotEmpty();
        return new KerberosEncryptedData(encryptionTypeNumber, keyVersionNumber, cipherText);
    }
}
