namespace Surl.Authentication;

/// <summary>
/// What <see cref="LdapAccountSource.CheckAsync"/> decided.
/// </summary>
/// <param name="IsAccepted">Whether the directory accepted the password.</param>
/// <param name="Reason">
/// For a refusal, the operator-facing reason (ADR-0149, decision 9) or <see langword="null"/> when the
/// directory was never asked; <see langword="null"/> for an acceptance. A peer never sees it.
/// </param>
public readonly record struct LdapLoginResult(bool IsAccepted, string? Reason);
