namespace Surl.Protocol.Ssh;

/// <summary>
/// What the connection's first key exchange leaves for packet protection (BL-161): the
/// algorithms agreed, the session identifier, and the derivation of the session keys.
/// </summary>
/// <param name="Algorithms">The algorithms agreed.</param>
/// <param name="SessionIdentifier">H of the first exchange, which never changes (RFC 4253, section 7.2).</param>
/// <param name="Keys">The six keys' derivation.</param>
internal sealed record SshKeyExchangeResult(SshNegotiatedAlgorithms Algorithms, byte[] SessionIdentifier, SshKeyDerivation Keys);
