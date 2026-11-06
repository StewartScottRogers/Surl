namespace Surl.Protocol.Abstractions;

/// <summary>
/// How an <see cref="IHttpAuthenticationSession"/> judged one request (ADR-0032, section 6).
/// The HTTP server decides nothing about methods: it acts on this verdict alone.
/// </summary>
/// <param name="Outcome">Whether the request is served, challenged or refused.</param>
/// <param name="WwwAuthenticateValues">
/// The <c>WWW-Authenticate</c> values to send, written as one field each, in order.
/// </param>
/// <param name="AccountName">
/// The account logged in, or <see langword="null"/> when the request is anonymous or refused. On
/// a verdict carrying a <see cref="BodyCheck"/>, the account whose signature the head already
/// verified, pending the body's check (AWS Signature Version 4 with a sent content hash, which
/// every S3 request has; ADR-0108 decision 4.4), and <see langword="null"/> when the signature
/// itself waits for the body.
/// </param>
/// <param name="CheckedLogin">
/// The credentials checked on this request and the answer, which the server writes to the
/// verbose log as <see cref="CheckedLogin.Note"/>; <see langword="null"/> when nothing was checked
/// (no credentials, a plain-text secret refused unchecked, a handshake's continuation step, a
/// login whose check waits for the body).
/// </param>
/// <param name="BodyCheck">
/// On a <see cref="HttpAuthenticationOutcome.Proceed"/> verdict, the rest of a login that binds
/// the body (ADR-0045): the server reads the body, hashes it, and serves the request only as the
/// verdict this check returns says. <see langword="null"/> when the head alone decides.
/// </param>
/// <param name="Refusal">
/// On a refused or challenged verdict, why, when the method that judged it can tell (AWS
/// Signature Version 4, ADR-0108 decision 4.4); <see langword="null"/> otherwise. The HTTP server
/// ignores it.
/// </param>
public sealed record HttpAuthenticationVerdict(
    HttpAuthenticationOutcome Outcome,
    IReadOnlyList<string> WwwAuthenticateValues,
    string? AccountName,
    CheckedLogin? CheckedLogin = null,
    IHttpRequestBodyCheck? BodyCheck = null,
    HttpAuthenticationRefusal? Refusal = null);
