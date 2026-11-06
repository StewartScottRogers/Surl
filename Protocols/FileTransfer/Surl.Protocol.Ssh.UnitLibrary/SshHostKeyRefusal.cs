namespace Surl.Protocol.Ssh;

/// <summary>
/// Why a host-key file's bytes give no host key, and the words ADR-0051 decision 4 writes
/// after <c>surl: (2) Host key &lt;path&gt;: </c>. The words hold no key material; a key type
/// taken from the file is as the file names it, for the caller to escape.
/// </summary>
/// <param name="Reason">Why.</param>
/// <param name="Text">The words after <c>Host key &lt;path&gt;: </c>.</param>
public sealed record SshHostKeyRefusal(SshHostKeyRefusalReason Reason, string Text)
{
    /// <summary><c>not a private key surl can read</c>.</summary>
    public static SshHostKeyRefusal NotAPrivateKey { get; } =
        new(SshHostKeyRefusalReason.NotAPrivateKey, "not a private key surl can read");

    /// <summary><c>the key is encrypted; give --pass</c>.</summary>
    public static SshHostKeyRefusal EncryptedWithoutPassphrase { get; } =
        new(SshHostKeyRefusalReason.EncryptedWithoutPassphrase, "the key is encrypted; give --pass");

    /// <summary><c>--pass does not decrypt the key</c>.</summary>
    public static SshHostKeyRefusal PassphraseDoesNotDecrypt { get; } =
        new(SshHostKeyRefusalReason.PassphraseDoesNotDecrypt, "--pass does not decrypt the key");

    /// <summary>
    /// <c>&lt;key type&gt; keys of &lt;bits&gt; bits need --allow-weak-ssh-algorithms</c>.
    /// </summary>
    /// <param name="keyType"><c>RSA</c> or <c>DSA</c>.</param>
    /// <param name="bits">The key's size in bits.</param>
    /// <returns>The refusal.</returns>
    public static SshHostKeyRefusal NeedsWeakAlgorithms(string keyType, long bits) =>
        new(SshHostKeyRefusalReason.NeedsWeakAlgorithms, $"{keyType} keys of {bits} bits need --allow-weak-ssh-algorithms");

    /// <summary>
    /// <c>key type &lt;type&gt; is not supported</c>.
    /// </summary>
    /// <param name="keyType">The type as the file names it: an SSH key type, or an algorithm or curve object identifier.</param>
    /// <returns>The refusal.</returns>
    public static SshHostKeyRefusal UnsupportedKeyType(string keyType) =>
        new(SshHostKeyRefusalReason.UnsupportedKeyType, $"key type {keyType} is not supported");
}
