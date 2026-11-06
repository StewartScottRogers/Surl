using Surl.Cryptography.Srp;

namespace Surl.TlsSrp;

/// <summary>
/// The account one handshake runs against: a known user's verifier, or an unknown user's simulated
/// one (ADR-0122 decision 4).
/// </summary>
/// <param name="UserName">The user name, as UTF-8 text.</param>
/// <param name="Group">The group the verifier is in.</param>
/// <param name="Salt">The salt <c>s</c> the ServerKeyExchange sends.</param>
/// <param name="Verifier">The verifier <c>v</c>, unsigned big-endian.</param>
/// <param name="IsSimulated">Whether the user is unknown and the account made up, so the handshake cannot complete.</param>
public sealed record TlsSrpAccount(string UserName, SrpGroup Group, byte[] Salt, byte[] Verifier, bool IsSimulated);
