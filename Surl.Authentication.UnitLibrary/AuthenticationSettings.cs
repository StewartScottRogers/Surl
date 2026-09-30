namespace Surl.Authentication;

/// <summary>
/// The accounts and the loosening options an <see cref="AuthenticationPolicy"/> applies
/// (ADR-0032, section 1), as <c>Surl.Console</c> composes them from the command line.
/// </summary>
/// <param name="Accounts">Every account from <c>--user</c> and <c>--user-file</c>.</param>
/// <param name="AllowAnonymous"><c>--allow-anonymous</c>: accept every request and login unchecked.</param>
/// <param name="AllowPlaintextAuthentication"><c>--allow-plaintext-auth</c>: accept plain-text secrets without TLS.</param>
/// <param name="AcceptedMethods">
/// <c>--auth</c>'s methods, <see cref="AuthenticationMethods.DefaultAccepted"/> when it is not given.
/// </param>
public sealed record AuthenticationSettings(
    AccountBook Accounts,
    bool AllowAnonymous,
    bool AllowPlaintextAuthentication,
    IReadOnlySet<AuthenticationMethod> AcceptedMethods);
