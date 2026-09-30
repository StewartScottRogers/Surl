using System.Numerics;

namespace Surl.Protocol.Ssh;

/// <summary>
/// The server's half of a finite-field Diffie-Hellman round (RFC 4253, section 8): given the
/// client's e, it draws y, computes f = g^y mod p and K = e^y mod p with
/// <see cref="BigInteger.ModPow"/>, after checking 1 &lt; e &lt; p - 1.
/// </summary>
internal static class SshDiffieHellman
{
    /// <summary>
    /// The length of y in bytes: 512 bits with the top bit set, at least twice the security of
    /// every RFC 3526 group (RFC 3526 section 8 estimates at most 200 bits for group 18) and of
    /// SHA-512.
    /// </summary>
    public const int PrivateExponentBytes = 64;

    /// <summary>
    /// Answers the client's public value.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="clientMpint">
    /// The bytes of the client's e <c>mpint</c> as sent, which H also covers as sent, so a
    /// client that pads e still gets a signature that verifies.
    /// </param>
    /// <param name="randomSource">Where y comes from.</param>
    /// <returns>f and K.</returns>
    /// <exception cref="SshDisconnectRequiredException">e is not in 1 &lt; e &lt; p - 1: <c>DISCONNECT</c> 2.</exception>
    public static (BigInteger ServerPublicValue, BigInteger SharedSecret) Answer(
        SshModpGroup group,
        ReadOnlySpan<byte> clientMpint,
        ISshRandomSource randomSource)
    {
        var clientPublicValue = new BigInteger(clientMpint, isUnsigned: false, isBigEndian: true);
        if (clientPublicValue <= BigInteger.One || clientPublicValue >= group.Prime - 1)
        {
            throw SshDisconnectRequiredException.ProtocolError(
                $"The client's Diffie-Hellman public value e is not in 1 < e < p - 1 of the {group.Bits}-bit group.");
        }

        var exponent = new byte[PrivateExponentBytes];
        randomSource.Fill(exponent);
        exponent[0] |= 0x80;
        var privateValue = new BigInteger(exponent, isUnsigned: true, isBigEndian: true);

        return (BigInteger.ModPow(group.Generator, privateValue, group.Prime), BigInteger.ModPow(clientPublicValue, privateValue, group.Prime));
    }
}
