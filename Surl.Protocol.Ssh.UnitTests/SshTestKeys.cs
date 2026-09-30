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
    /// A set holding the RSA key and the three ECDSA keys, so every host-key algorithm is offered.
    /// </summary>
    public static SshHostKeySet AllHostKeys() => HostKeysOf(Rsa2048, EcdsaP256, EcdsaP384, EcdsaP521);

    private static T Made<T>(T key)
        where T : AsymmetricAlgorithm
    {
        key.ExportPkcs8PrivateKey();

        return key;
    }
}
