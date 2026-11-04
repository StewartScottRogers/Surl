namespace Surl.Protocol.Ssh;

/// <summary>
/// Why a host-key file's bytes give no host key (ADR-0051, decision 4's refusals).
/// </summary>
public enum SshHostKeyRefusalReason
{
    /// <summary>Not one private key in a format surl reads.</summary>
    NotAPrivateKey,

    /// <summary>An encrypted key, and no passphrase was given.</summary>
    EncryptedWithoutPassphrase,

    /// <summary>An encrypted key the passphrase given does not decrypt.</summary>
    PassphraseDoesNotDecrypt,

    /// <summary>An RSA key under 2048 bits, or a DSA key, without <c>--allow-weak-ssh-algorithms</c>.</summary>
    NeedsWeakAlgorithms,

    /// <summary>A key type surl does not serve.</summary>
    UnsupportedKeyType,
}
