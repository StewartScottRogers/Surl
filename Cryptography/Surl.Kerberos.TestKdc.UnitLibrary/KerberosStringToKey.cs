using System.Security.Cryptography;
using System.Text;

namespace Surl.Kerberos.TestKdc;

/// <summary>
/// Turns a password into a user's long-term key, as a KDC and the client's Kerberos library both
/// do: RFC 3962 section 4 for enctypes 17 and 18 (PBKDF2-HMAC-SHA1 over the salt, then
/// <c>DK(tkey, "kerberos")</c>), RFC 8009 section 4 for 19 and 20 (PBKDF2-HMAC-SHA256 or -SHA384
/// over the enctype's name, a zero byte and the salt, then <c>KDF-HMAC-SHA2(tkey, "kerberos")</c>).
/// </summary>
internal static class KerberosStringToKey
{
    /// <summary>RFC 3962 section 4's default iteration count.</summary>
    public const int Rfc3962DefaultIterationCount = 4096;

    /// <summary>RFC 8009 section 4's default iteration count.</summary>
    public const int Rfc8009DefaultIterationCount = 32768;

    private static readonly byte[] KerberosConstant = "kerberos"u8.ToArray();

    /// <summary>Gets RFC 4120 section 4's default salt: the realm, then every name component, concatenated.</summary>
    /// <param name="principal">The user.</param>
    /// <returns>The salt.</returns>
    public static string DefaultSalt(KerberosPrincipalName principal) => principal.Realm + string.Concat(principal.Components);

    /// <summary>Derives the key with the enctype's default iteration count.</summary>
    /// <param name="encryptionType">The key's enctype.</param>
    /// <param name="password">The password.</param>
    /// <param name="salt">The salt.</param>
    /// <returns>The key, as long as the enctype's keys.</returns>
    public static byte[] DeriveKey(KerberosEncryptionType encryptionType, string password, string salt) =>
        DeriveKey(encryptionType, Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(salt), IsRfc8009(encryptionType) ? Rfc8009DefaultIterationCount : Rfc3962DefaultIterationCount);

    /// <summary>Derives the key with <paramref name="iterationCount" /> PBKDF2 iterations, as the RFCs' test vectors vary it.</summary>
    /// <param name="encryptionType">The key's enctype.</param>
    /// <param name="password">The password's bytes.</param>
    /// <param name="salt">The salt's bytes.</param>
    /// <param name="iterationCount">The PBKDF2 iteration count.</param>
    /// <returns>The key, as long as the enctype's keys.</returns>
    public static byte[] DeriveKey(KerberosEncryptionType encryptionType, byte[] password, byte[] salt, int iterationCount)
    {
        int keyLength = KerberosEncryptionProfile.For(encryptionType).KeyLength;
        if (!IsRfc8009(encryptionType))
        {
            byte[] rfc3962Key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterationCount, HashAlgorithmName.SHA1, keyLength);
            return SimplifiedProfileKeyDerivation.DeriveKey(rfc3962Key, KerberosConstant);
        }

        HashAlgorithmName hashAlgorithm = encryptionType == KerberosEncryptionType.Aes128CtsHmacSha256128 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA384;
        byte[] saltWithEncryptionTypeName = [.. Encoding.ASCII.GetBytes(KerberosAcceptor.NameEncryptionType((int)encryptionType)), 0, .. salt];
        byte[] rfc8009Key = Rfc2898DeriveBytes.Pbkdf2(password, saltWithEncryptionTypeName, iterationCount, hashAlgorithm, keyLength);
        return KdfHmacSha2.Derive(hashAlgorithm, rfc8009Key, KerberosConstant, keyLength);
    }

    private static bool IsRfc8009(KerberosEncryptionType encryptionType) =>
        encryptionType is KerberosEncryptionType.Aes128CtsHmacSha256128 or KerberosEncryptionType.Aes256CtsHmacSha384192;
}
