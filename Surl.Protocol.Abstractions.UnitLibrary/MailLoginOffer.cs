namespace Surl.Protocol.Abstractions;

/// <summary>
/// What a mail server advertises on one connection, from
/// <see cref="IMailAuthenticationPolicy.GetMailLoginOffer"/> (ADR-0049, section 2).
/// </summary>
/// <param name="SaslMechanisms">
/// The SASL mechanisms' registered names, upper case, in the order ADR-0049 section 2 lists them;
/// empty when none is offered.
/// </param>
/// <param name="IsClearPasswordLoginOffered">
/// Whether IMAP's <c>LOGIN</c> (otherwise <c>LOGINDISABLED</c>) and POP3's <c>USER</c>/<c>PASS</c>
/// are offered.
/// </param>
/// <param name="IsApopOffered">Whether POP3's greeting carries a timestamp for <c>APOP</c>.</param>
public sealed record MailLoginOffer(
    IReadOnlyList<string> SaslMechanisms,
    bool IsClearPasswordLoginOffered,
    bool IsApopOffered);
