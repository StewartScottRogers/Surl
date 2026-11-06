namespace Surl.Protocol.Abstractions;

/// <summary>
/// Who may log in to the SMB server (ADR-0073, decision 3). It sits beside
/// <see cref="IAuthenticationPolicy"/>; <c>Surl.Console</c> passes the same object to the SMB
/// server as this interface. The server owns the SMB version 1 framing, the server challenge and
/// the answer to the session setup; the policy owns which accounts exist, whether <c>ntlmv1</c>
/// is in <c>--auth</c>, the NTLMv1 calculation and comparison, ADR-0032 section 8's delay and the
/// login note.
/// </summary>
public interface ISmbAuthenticationPolicy
{
    /// <summary>
    /// Judges the NTLMv1 responses of an SMB session setup against the server challenge the
    /// negotiate response sent.
    /// </summary>
    /// <param name="login">The session setup as the client sent it, with the server challenge.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Whether the login is accepted, with the note to write when a credential was checked.</returns>
    ValueTask<SmbLoginVerdict> CheckSmbNtlmV1LoginAsync(SmbNtlmV1Login login, CancellationToken cancellationToken);
}
