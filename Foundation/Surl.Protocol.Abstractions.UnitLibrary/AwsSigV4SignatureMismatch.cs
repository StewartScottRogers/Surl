namespace Surl.Protocol.Abstractions;

/// <summary>
/// What the server computed for an AWS Signature Version 4 signature that did not match
/// (<see cref="HttpAuthenticationRefusal.BadSignature"/>), so a server can tell the client why its
/// signature differs, as AWS's <c>SignatureDoesNotMatch</c> error does. Both are built from the
/// client's own request and the public algorithm and hold no secret of the server's. Each character
/// is one byte of the request as received (Latin-1).
/// </summary>
/// <param name="CanonicalRequest">
/// The canonical request, lines joined by line feeds, with <c>****</c> in place of each value that
/// the exchange log withholds (ADR-0182): a signed <c>Authorization</c>, <c>Proxy-Authorization</c>,
/// API-key or <c>x-amz-security-token</c> header, and an <c>X-Amz-Security-Token</c> query parameter.
/// </param>
/// <param name="StringToSign">
/// The string to sign, lines joined by line feeds. Its last line is the SHA-256 of the canonical
/// request as signed, with nothing withheld, so it is not the hash of
/// <paramref name="CanonicalRequest"/> when a value there is <c>****</c>; whoever holds both can
/// test guesses of a withheld value against it, which matters only for a guessable one.
/// </param>
public sealed record AwsSigV4SignatureMismatch(string CanonicalRequest, string StringToSign);
