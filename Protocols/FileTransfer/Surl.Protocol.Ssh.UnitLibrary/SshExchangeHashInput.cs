using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The bytes the exchange hash H is computed over. Every method starts them the same way -
/// <c>V_C</c>, <c>V_S</c>, <c>I_C</c>, <c>I_S</c> and <c>K_S</c> as <c>string</c>s (RFC 4253
/// section 8) - appends its own fields to <see cref="Fields"/>, and ends them with the shared
/// secret K as an <c>mpint</c>.
/// </summary>
internal sealed class SshExchangeHashInput
{
    /// <summary>
    /// Starts the input with the fields every method shares.
    /// </summary>
    /// <param name="clientIdentification"><c>V_C</c>: the client's identification line without its ending.</param>
    /// <param name="serverIdentification"><c>V_S</c>: the server's identification line without its ending.</param>
    /// <param name="clientKexInit"><c>I_C</c>: the payload of the client's <c>KEXINIT</c>, as received.</param>
    /// <param name="serverKexInit"><c>I_S</c>: the payload of the server's <c>KEXINIT</c>, as sent.</param>
    /// <param name="hostKeyBlob"><c>K_S</c>: the server's public host key blob.</param>
    public SshExchangeHashInput(
        ReadOnlySpan<byte> clientIdentification,
        ReadOnlySpan<byte> serverIdentification,
        ReadOnlySpan<byte> clientKexInit,
        ReadOnlySpan<byte> serverKexInit,
        ReadOnlySpan<byte> hostKeyBlob)
    {
        Fields.WriteString(clientIdentification);
        Fields.WriteString(serverIdentification);
        Fields.WriteString(clientKexInit);
        Fields.WriteString(serverKexInit);
        Fields.WriteString(hostKeyBlob);
    }

    /// <summary>
    /// The writer a method appends its own fields to, after <c>K_S</c>.
    /// </summary>
    public SshWireWriter Fields { get; } = new();

    /// <summary>
    /// Appends K and hashes everything.
    /// </summary>
    /// <param name="sharedSecret">K.</param>
    /// <param name="hashAlgorithm">The method's hash.</param>
    /// <returns>H.</returns>
    public byte[] ComputeHash(BigInteger sharedSecret, HashAlgorithmName hashAlgorithm)
    {
        Fields.WriteMpint(sharedSecret);

        return CryptographicOperations.HashData(hashAlgorithm, Fields.ToArray());
    }
}
