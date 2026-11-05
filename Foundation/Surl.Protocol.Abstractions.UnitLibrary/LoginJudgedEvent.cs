namespace Surl.Protocol.Abstractions;

/// <summary>
/// A login was judged. It never carries a password, hash, token or any other secret.
/// </summary>
/// <param name="ExchangeId">The exchange the login was made on, or <see langword="null"/> when none is known.</param>
/// <param name="Scheme">The scheme the login was made over.</param>
/// <param name="Account">The account name the client gave.</param>
/// <param name="Mechanism">The login method or mechanism (<c>Basic</c>, <c>PLAIN</c>, <c>password</c>).</param>
/// <param name="IsAccepted">Whether the login was accepted.</param>
/// <param name="IsRefusedByFault">Whether a <see cref="FaultKind.RefuseLogin"/> fault refused it.</param>
public sealed record LoginJudgedEvent(
    long? ExchangeId,
    string Scheme,
    string Account,
    string Mechanism,
    bool IsAccepted,
    bool IsRefusedByFault) : SurlEvent;
