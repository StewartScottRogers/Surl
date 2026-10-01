using System.Text;

namespace Surl.Protocol.Abstractions;

/// <summary>
/// An SMB version 1 session setup's NTLMv1 login, as the SMB server hands it to
/// <see cref="ISmbAuthenticationPolicy.CheckSmbNtlmV1LoginAsync"/> (ADR-0073, decision 3).
/// <see cref="ToString"/> shows the user and domain, never the challenge or a response.
/// </summary>
/// <param name="UserName">The user name as sent, ASCII-decoded.</param>
/// <param name="DomainName">The primary domain as sent; for the log only, never matched.</param>
/// <param name="ServerChallenge">The 8-byte challenge the server's negotiate response sent.</param>
/// <param name="LmResponse">The LM response as sent; ignored by the check.</param>
/// <param name="NtResponse">The NT response as sent, 24 bytes when well formed.</param>
/// <param name="TlsSession">The TLS session on <c>smbs://</c>, <see langword="null"/> on <c>smb://</c>; for the log only.</param>
public sealed record SmbNtlmV1Login(
    string UserName,
    string DomainName,
    ReadOnlyMemory<byte> ServerChallenge,
    ReadOnlyMemory<byte> LmResponse,
    ReadOnlyMemory<byte> NtResponse,
    TlsSession? TlsSession)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("UserName = ").Append(UserName).Append(", DomainName = ").Append(DomainName);
        return true;
    }
}
