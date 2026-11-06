using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Private keys the tests share, each made once per test run by the BCL (RSA is slow to make),
/// except the DSA key, which is imported from fixed bytes.
/// </summary>
internal static class SshTestKeys
{
    // A BCL key is made when first used, so each is exported once here: tests running in
    // parallel would otherwise race to make it, and see two different keys.
    private static readonly Lazy<RSA> Rsa2048Key = new(() => Made(RSA.Create(2048)));

    private static readonly Lazy<RSA> Rsa1024Key = new(() => Made(RSA.Create(1024)));

    // A fixed key, imported rather than made: macOS's BCL can import a DSA key but not make one.
    private static readonly Lazy<DSA> Dsa1024Key = new(() => Made(ImportedDsa(Dsa1024Pkcs8)));

    private static readonly Lazy<ECDsa> EcdsaP256Key = new(() => Made(ECDsa.Create(ECCurve.NamedCurves.nistP256)));

    private static readonly Lazy<ECDsa> EcdsaP384Key = new(() => Made(ECDsa.Create(ECCurve.NamedCurves.nistP384)));

    private static readonly Lazy<ECDsa> EcdsaP521Key = new(() => Made(ECDsa.Create(ECCurve.NamedCurves.nistP521)));

    /// <summary>A 1024-bit DSA private key in PKCS #8, made once on Windows by the BCL.</summary>
    private const string Dsa1024Pkcs8 =
        "MIIBSwIBADCCASwGByqGSM44BAEwggEfAoGBAMGjJPLvrwPXriablslJvZsdQJn5cX0Sqm7MMCzMp7pqk4yUEIzXT3OBWbPIDiMMg2P9Y6Mg9n/VX41J" +
        "qC5MXzHqRc2XDN3kFUMrHhtETaJCfFSF0QP2Ih6JiB4ktdjbVOmVkeip05N/zsdsy+VFW6CfUpdUKf76E2tIQMYYlMfXAhUA7gXZZlYqbpoP+qRKVbjw" +
        "4Xt2hZ0CgYEArVOFEWBWUd2wSOAwrnEHyArgKttU4/jhxZxpQWG1kei8gjPuIcDLbIMK/Y70wfidvCToBNhJ2NnpP3bPecZ9WSFa4eL73cETNXw3+KIi" +
        "XzgorxQSunIejw0gYtcSJ4pRXsqpYgzRdt0GQEVdy7dPlT0BOsUYOGcoQeK90ENzCJsEFgIU7Dtuyo0h32F4q9rN4OFRzV6oSMc=";

    public static RSA Rsa2048 => Rsa2048Key.Value;

    public static RSA Rsa1024 => Rsa1024Key.Value;

    public static DSA Dsa1024 => Dsa1024Key.Value;

    public static ECDsa EcdsaP256 => EcdsaP256Key.Value;

    public static ECDsa EcdsaP384 => EcdsaP384Key.Value;

    public static ECDsa EcdsaP521 => EcdsaP521Key.Value;

    /// <summary>The secret key of RFC 8032 section 7.1's TEST 1, an Ed25519 seed.</summary>
    public static byte[] Ed25519Seed => Convert.FromHexString("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60");

    /// <summary>The public key of RFC 8032 section 7.1's TEST 1, <see cref="Ed25519Seed"/>'s.</summary>
    public static byte[] Ed25519PublicKey => Convert.FromHexString("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a");

    /// <summary>The host key of <see cref="Ed25519Seed"/>.</summary>
    public static SshHostKey Ed25519HostKey => SshEd25519HostKey.FromSeed(Ed25519Seed);

    /// <summary>
    /// A host-key set holding the host key of each key given.
    /// </summary>
    public static SshHostKeySet HostKeysOf(params AsymmetricAlgorithm[] keys)
    {
        var set = new SshHostKeySet();
        foreach (var key in keys)
        {
            set.TryAdd(key switch { RSA rsa => SshHostKey.FromRsa(rsa), DSA dsa => SshHostKey.FromDsa(dsa), _ => SshHostKey.FromEcdsa((ECDsa)key) }, out _);
        }

        return set;
    }

    /// <summary>
    /// A set holding the RSA key, the three ECDSA keys and the Ed25519 key, so every host-key
    /// algorithm is offered.
    /// </summary>
    public static SshHostKeySet AllHostKeys()
    {
        var set = HostKeysOf(Rsa2048, EcdsaP256, EcdsaP384, EcdsaP521);
        set.TryAdd(Ed25519HostKey, out _);

        return set;
    }

    private static DSA ImportedDsa(string pkcs8)
    {
        var dsa = DSA.Create();
        dsa.ImportPkcs8PrivateKey(Convert.FromBase64String(pkcs8), out _);

        return dsa;
    }

    private static T Made<T>(T key)
        where T : AsymmetricAlgorithm
    {
        key.ExportPkcs8PrivateKey();

        return key;
    }
}
