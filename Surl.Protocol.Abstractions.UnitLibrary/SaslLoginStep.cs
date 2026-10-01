namespace Surl.Protocol.Abstractions;

/// <summary>
/// One step of a SASL login, from an <see cref="ISaslExchange"/> or
/// <see cref="IMailAuthenticationPolicy.CheckApopLoginAsync"/> (ADR-0049, section 6; ADR-0072,
/// decision 4). The server decides nothing about mechanisms: it acts on this step alone.
/// </summary>
/// <param name="Outcome">Whether to send a challenge, or how the login ended.</param>
/// <param name="Challenge">
/// The continuation's bytes before base64; empty unless <paramref name="Outcome"/> is
/// <see cref="SaslLoginOutcome.Challenge"/>.
/// </param>
/// <param name="AccountName">
/// The account logged in when <see cref="SaslLoginOutcome.Accepted"/>; otherwise <see langword="null"/>.
/// </param>
/// <param name="CheckedLogin">
/// The credentials checked on this step and the answer, which the server writes to the verbose log
/// as <see cref="CheckedLogin.Note"/> before it answers; <see langword="null"/> when nothing was checked.
/// </param>
/// <param name="RefusalNote">
/// Why the credentials were refused, as the server writes it to the verbose log after
/// <see cref="CheckedLogin.Note"/>, such as <c>Kerberos: ticket expired</c> (ADR-0057, decision 4);
/// <see langword="null"/> when the mechanism names no reason. It never holds a key byte, a password
/// or a decrypted field.
/// </param>
/// <param name="SecurityLayer">
/// The security layer an accepted login negotiated, which protects every message after the
/// server's answer to this step (ADR-0072, decision 4); <see langword="null"/> when none was, and
/// always when <see cref="SaslExchangeStart.CanCarrySecurityLayer"/> was <see langword="false"/>.
/// </param>
public sealed record SaslLoginStep(
    SaslLoginOutcome Outcome,
    ReadOnlyMemory<byte> Challenge,
    string? AccountName,
    CheckedLogin? CheckedLogin,
    string? RefusalNote = null,
    ISaslSecurityLayer? SecurityLayer = null);
