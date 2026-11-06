namespace Surl.Protocol.Abstractions;

/// <summary>
/// An SSH <c>publickey</c> request (RFC 4252 section 7), as the SSH server hands it to
/// <see cref="ISshAuthenticationPolicy.CheckSshPublicKeyLoginAsync"/> (ADR-0051, section 7).
/// </summary>
/// <param name="UserName">The user name as sent, or <see langword="null"/> when it is not UTF-8.</param>
/// <param name="SignatureAlgorithm">The algorithm name the request carries, e.g. <c>rsa-sha2-256</c>.</param>
/// <param name="PublicKeyBlob">The public key blob as sent (RFC 4253 section 6.6).</param>
/// <param name="Proof">Whether the request is a query or signed, and whether the server verified its signature.</param>
public sealed record SshPublicKeyLogin(
    string? UserName,
    string SignatureAlgorithm,
    ReadOnlyMemory<byte> PublicKeyBlob,
    SshPublicKeyProof Proof);
