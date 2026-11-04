using System.Formats.Asn1;

namespace Surl.Kerberos;

/// <summary>
/// RFC 4120 section 5.2.9's <c>EncryptionKey</c>: a ticket's session key or an authenticator's
/// subkey.
/// </summary>
/// <param name="KeyTypeNumber">The <c>keytype</c>, an enctype number.</param>
/// <param name="KeyValue">The <c>keyvalue</c>.</param>
internal sealed record KerberosEncryptionKey(int KeyTypeNumber, byte[] KeyValue)
{
    /// <summary>Reads the field <c>[n] EncryptionKey</c>.</summary>
    /// <param name="fields">The enclosing <c>SEQUENCE</c>'s fields.</param>
    /// <param name="number">The field's tag number.</param>
    /// <returns>The key.</returns>
    public static KerberosEncryptionKey ReadField(AsnReader fields, int number)
    {
        AsnReader field = KerberosDer.ReadField(fields, number);
        AsnReader encryptionKey = field.ReadSequence();
        field.ThrowIfNotEmpty();
        int keyTypeNumber = KerberosDer.ReadInt32Field(encryptionKey, 0);
        byte[] keyValue = KerberosDer.ReadOctetStringField(encryptionKey, 1);
        encryptionKey.ThrowIfNotEmpty();
        return new KerberosEncryptionKey(keyTypeNumber, keyValue);
    }

    /// <summary>
    /// Gets the profile of this key's encryption type, refusing a type surl does not accept
    /// (ADR-0057 decision 3) and a key whose length is not that type's.
    /// </summary>
    /// <returns>The profile.</returns>
    /// <exception cref="KerberosRefusalException">The key's type is not one surl accepts.</exception>
    /// <exception cref="AsnContentException">The key is not as long as its type's keys.</exception>
    public KerberosEncryptionProfile GetProfile()
    {
        if (!Enum.IsDefined((KerberosEncryptionType)KeyTypeNumber))
        {
            throw new KerberosRefusalException($"unsupported enctype {KeyTypeNumber}");
        }

        KerberosEncryptionProfile profile = KerberosEncryptionProfile.For((KerberosEncryptionType)KeyTypeNumber);
        if (KeyValue.Length != profile.KeyLength)
        {
            throw new AsnContentException("The key is not as long as its encryption type's keys.");
        }

        return profile;
    }
}
