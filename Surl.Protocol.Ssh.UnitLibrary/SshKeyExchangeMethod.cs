using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The server's side of one key exchange method: it reads the client's messages, answers
/// with the host key, its own public value and its signature over the exchange hash H, and
/// hands back K and H (RFC 4253, section 7).
/// </summary>
/// <param name="hashAlgorithm">The method's hash, which H and the session keys use.</param>
internal abstract class SshKeyExchangeMethod(HashAlgorithmName hashAlgorithm)
{
    private static readonly Dictionary<string, Func<ISshRandomSource, SshKeyExchangeMethod>> Methods = new(StringComparer.Ordinal)
    {
        ["ecdh-sha2-nistp256"] = _ => new SshEcdhKeyExchange(SshNistCurve.NistP256),
        ["ecdh-sha2-nistp384"] = _ => new SshEcdhKeyExchange(SshNistCurve.NistP384),
        ["ecdh-sha2-nistp521"] = _ => new SshEcdhKeyExchange(SshNistCurve.NistP521),
        ["diffie-hellman-group14-sha256"] = random => new SshFiniteFieldKeyExchange(SshModpGroup.Group14, HashAlgorithmName.SHA256, random),
        ["diffie-hellman-group16-sha512"] = random => new SshFiniteFieldKeyExchange(SshModpGroup.Group16, HashAlgorithmName.SHA512, random),
        ["diffie-hellman-group18-sha512"] = random => new SshFiniteFieldKeyExchange(SshModpGroup.Group18, HashAlgorithmName.SHA512, random),
        ["diffie-hellman-group-exchange-sha256"] = random => new SshGroupExchangeKeyExchange(random),
    };

    /// <summary>
    /// The method's hash.
    /// </summary>
    public HashAlgorithmName HashAlgorithm { get; } = hashAlgorithm;

    /// <summary>
    /// The server's side of the method BL-160 builds for <paramref name="name"/>:
    /// <c>ecdh-sha2-nistp256</c>, <c>-nistp384</c> and <c>-nistp521</c> (RFC 5656),
    /// <c>diffie-hellman-group14-sha256</c>, <c>group16-sha512</c> and <c>group18-sha512</c>
    /// (RFC 8268), and <c>diffie-hellman-group-exchange-sha256</c> (RFC 4419).
    /// </summary>
    /// <param name="name">The method agreed.</param>
    /// <param name="randomSource">Where a finite-field method's private exponent comes from.</param>
    /// <returns>The method, or <see langword="null"/> for one not built yet (<c>curve25519-sha256</c> is BL-167's).</returns>
    public static SshKeyExchangeMethod? ForName(string name, ISshRandomSource randomSource) =>
        Methods.TryGetValue(name, out var create) ? create(randomSource) : null;

    /// <summary>
    /// Runs the method's messages up to, not including, <c>NEWKEYS</c>.
    /// </summary>
    /// <param name="channel">The key exchange's messages.</param>
    /// <param name="hashInput">H's input, up to <c>K_S</c>.</param>
    /// <param name="hostKey">The host key that signs H.</param>
    /// <param name="hostKeyAlgorithm">The host-key algorithm agreed, which it signs with.</param>
    /// <param name="cancellationToken">Cuts the exchange off.</param>
    /// <returns>K and H.</returns>
    /// <exception cref="SshDisconnectRequiredException">The client's message is refused.</exception>
    public abstract ValueTask<SshKeyExchangeOutput> RunAsync(
        ISshKeyExchangeChannel channel,
        SshExchangeHashInput hashInput,
        SshHostKey hostKey,
        string hostKeyAlgorithm,
        CancellationToken cancellationToken);

    /// <summary>
    /// Computes H over <paramref name="hashInput"/> and K, and writes the reply every method
    /// ends with: <paramref name="replyNumber"/>, <c>K_S</c>, the server's public value, and
    /// the signature of H.
    /// </summary>
    /// <param name="channel">Where the reply goes.</param>
    /// <param name="replyNumber">The reply's message number.</param>
    /// <param name="hashInput">H's input, with every field but K.</param>
    /// <param name="sharedSecret">K.</param>
    /// <param name="hostKey">The host key.</param>
    /// <param name="hostKeyAlgorithm">The algorithm it signs with.</param>
    /// <param name="writeServerValue">Writes the server's public value into the reply.</param>
    /// <param name="cancellationToken">Cuts the write off.</param>
    /// <returns>K and H.</returns>
    private protected async ValueTask<SshKeyExchangeOutput> ReplyAsync(
        ISshKeyExchangeChannel channel,
        byte replyNumber,
        SshExchangeHashInput hashInput,
        BigInteger sharedSecret,
        SshHostKey hostKey,
        string hostKeyAlgorithm,
        Action<SshWireWriter> writeServerValue,
        CancellationToken cancellationToken)
    {
        var exchangeHash = hashInput.ComputeHash(sharedSecret, HashAlgorithm);
        var reply = new SshWireWriter();
        reply.WriteByte(replyNumber);
        reply.WriteString(hostKey.PublicKeyBlob.Span);
        writeServerValue(reply);
        reply.WriteString(hostKey.Sign(hostKeyAlgorithm, exchangeHash));
        await channel.WriteAsync(reply.ToArray(), cancellationToken);

        return new SshKeyExchangeOutput(sharedSecret, exchangeHash);
    }
}
