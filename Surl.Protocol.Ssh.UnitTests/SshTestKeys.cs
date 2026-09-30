using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// Private keys the tests share, each made once per test run by the BCL: RSA is slow to make.
/// </summary>
internal static class SshTestKeys
{
    // A BCL key is made when first used, so each is exported once here: tests running in
    // parallel would otherwise race to make it, and see two different keys.
    private static readonly Lazy<RSA> Rsa2048Key = new(() => Made(RSA.Create(2048)));

    private static readonly Lazy<RSA> Rsa1024Key = new(() => Made(RSA.Create(1024)));

    private static readonly Lazy<ECDsa> EcdsaP256Key = new(() => Made(ECDsa.Create(ECCurve.NamedCurves.nistP256)));

    private static readonly Lazy<ECDsa> EcdsaP384Key = new(() => Made(ECDsa.Create(ECCurve.NamedCurves.nistP384)));

    private static readonly Lazy<ECDsa> EcdsaP521Key = new(() => Made(ECDsa.Create(ECCurve.NamedCurves.nistP521)));

    public static RSA Rsa2048 => Rsa2048Key.Value;

    public static RSA Rsa1024 => Rsa1024Key.Value;

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
            set.TryAdd(key is RSA rsa ? SshHostKey.FromRsa(rsa) : SshHostKey.FromEcdsa((ECDsa)key), out _);
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

    private static T Made<T>(T key)
        where T : AsymmetricAlgorithm
    {
        key.ExportPkcs8PrivateKey();

        return key;
    }
}
