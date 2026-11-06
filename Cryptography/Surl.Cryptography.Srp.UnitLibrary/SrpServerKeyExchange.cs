using System.Numerics;
using System.Security.Cryptography;

namespace Surl.Cryptography.Srp;

/// <summary>
/// The server's half of one SRP-6a key exchange (RFC 5054 sections 2.5.3 to 2.6): draws the
/// secret <c>b</c> once, offers <c>B</c>, and turns the client's <c>A</c> into the premaster secret.
/// One instance serves one handshake.
/// </summary>
public sealed class SrpServerKeyExchange
{
    /// <summary>The length of the secret <c>b</c> in bytes: RFC 5054 section 2.5.3 asks for at least 256 bits.</summary>
    public const int ServerPrivateValueLength = 32;

    private readonly BigInteger verifier;
    private readonly BigInteger serverPrivateValue;
    private readonly BigInteger serverPublicValue;

    /// <summary>
    /// Starts an exchange for one user's verifier, drawing <c>b</c> from <paramref name="random" />.
    /// </summary>
    /// <param name="group">The group the verifier was computed in.</param>
    /// <param name="verifier">The user's verifier <c>v</c>, unsigned big-endian.</param>
    /// <param name="random">The source of <c>b</c>; tests pass a fixed one.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    public SrpServerKeyExchange(SrpGroup group, ReadOnlySpan<byte> verifier, RandomNumberGenerator random)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(random);
        Group = group;
        this.verifier = Srp6a.FromUnsignedBigEndian(verifier);
        var secret = new byte[ServerPrivateValueLength];
        random.GetBytes(secret);
        serverPrivateValue = Srp6a.FromUnsignedBigEndian(secret);
        CryptographicOperations.ZeroMemory(secret);
        serverPublicValue = Srp6a.ComputeServerPublicValue(group, this.verifier, serverPrivateValue);
    }

    /// <summary>The group of this exchange.</summary>
    public SrpGroup Group { get; }

    /// <summary>The server's public value <c>B</c>, unsigned big-endian with no leading zero byte.</summary>
    public byte[] ServerPublicValue => Srp6a.ToUnsignedBigEndian(serverPublicValue);

    /// <summary>
    /// Computes the premaster secret from the client's <c>A</c>, or refuses an <c>A</c> with
    /// <c>A % N = 0</c>, which the server answers with <c>illegal_parameter</c> (RFC 5054 section 2.5.4).
    /// </summary>
    /// <param name="clientPublicValue">The client's <c>A</c>, unsigned big-endian, no longer than <c>N</c>.</param>
    /// <param name="premasterSecret">
    /// The premaster secret, unsigned big-endian with no leading zero byte, as OpenSSL's client
    /// computes it; empty when refused.
    /// </param>
    /// <returns>Whether <c>A</c> was acceptable.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><c>A</c> is longer than <c>N</c> (a <c>decode_error</c> the caller checks first).</exception>
    public bool TryComputePremasterSecret(ReadOnlySpan<byte> clientPublicValue, out byte[] premasterSecret)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(clientPublicValue.Length, Group.PrimeLength, nameof(clientPublicValue));
        var clientValue = Srp6a.FromUnsignedBigEndian(clientPublicValue);
        if (!Srp6a.IsClientPublicValueAcceptable(Group, clientValue))
        {
            premasterSecret = [];
            return false;
        }

        var scrambler = Srp6a.ComputeScrambler(Group, clientValue, serverPublicValue);
        premasterSecret = Srp6a.ToUnsignedBigEndian(
            Srp6a.ComputePremasterSecret(Group, clientValue, verifier, scrambler, serverPrivateValue));
        return true;
    }
}
