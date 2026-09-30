namespace Surl.Protocol.Abstractions;

/// <summary>
/// One step of a mail login, from an <see cref="ISaslExchange"/> or
/// <see cref="IMailAuthenticationPolicy.CheckApopLoginAsync"/> (ADR-0049, section 6). The server
/// decides nothing about mechanisms: it acts on this step alone.
/// </summary>
/// <param name="Outcome">Whether to send a challenge, or how the login ended.</param>
/// <param name="Challenge">
/// The continuation's bytes before base64; empty unless <paramref name="Outcome"/> is
/// <see cref="MailLoginOutcome.Challenge"/>.
/// </param>
/// <param name="AccountName">
/// The account logged in when <see cref="MailLoginOutcome.Accepted"/>; otherwise <see langword="null"/>.
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
public sealed record MailLoginStep(
    MailLoginOutcome Outcome,
    ReadOnlyMemory<byte> Challenge,
    string? AccountName,
    CheckedLogin? CheckedLogin,
    string? RefusalNote = null);
