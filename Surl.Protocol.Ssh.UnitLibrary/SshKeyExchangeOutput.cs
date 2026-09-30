using System.Numerics;

namespace Surl.Protocol.Ssh;

/// <summary>
/// What a key exchange method agrees: the shared secret K and the exchange hash H.
/// </summary>
/// <param name="SharedSecret">K.</param>
/// <param name="ExchangeHash">H.</param>
internal sealed record SshKeyExchangeOutput(BigInteger SharedSecret, byte[] ExchangeHash);
