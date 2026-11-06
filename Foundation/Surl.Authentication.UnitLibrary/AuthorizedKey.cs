namespace Surl.Authentication;

/// <summary>
/// One public key a user may log in to the SSH server with, from an <c>--authorized-keys</c>
/// file (ADR-0051, section 6). A public key is not a secret, so it may be named in a log.
/// </summary>
/// <param name="UserName">The account the key logs in as: the user part of <c>--authorized-keys &lt;user:file&gt;</c>.</param>
/// <param name="KeyType">The key type the line names, e.g. <c>ssh-ed25519</c>; the blob's own type string equals it.</param>
/// <param name="Blob">The public key blob (RFC 4253 section 6.6), as the line's base64 decodes.</param>
public sealed record AuthorizedKey(string UserName, string KeyType, ReadOnlyMemory<byte> Blob);
