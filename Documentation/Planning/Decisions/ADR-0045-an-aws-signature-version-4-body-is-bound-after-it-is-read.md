# ADR-0045 — An AWS Signature Version 4 body is bound to its signature once it is read

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-136.
- **Amends:** [ADR-0043](ADR-0043-http-aws-signature-version-4-check.md), decision 6, and
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) section 4,
  "Where it is judged", for this one method.

## Context

AWS Signature Version 4 signs the SHA-256 of the request's body. ADR-0043 judged the login on
the head alone (ADR-0032 section 6), so a signed `s3` write was accepted without its body being
compared with the signed `x-amz-content-sha256`, and a write for any other service, which sends
no such field, was refused outright. The HTTP server judges every login before it reads a body
byte (ADR-0032 section 4), so the body can only be bound by a check after it.

### Measured

Pinned upstream curl 8.21.0 (win-x64 reference build), `Record-CurlExchange.ps1 -Port 18136`,
`-u AKIDEXAMPLE:secret`, a file `<file>` holding `body`; the fixtures are in
`Surl.Authentication.UnitTests/Fixtures` (its README lists every command line).

- `-H 'x-amz-content-sha256: UNSIGNED-PAYLOAD' -X PUT -d body` with `aws:amz:us-east-1:s3`
  sends the field once, as given, and signs `UNSIGNED-PAYLOAD` as the payload hash
  (`aws-sigv4-unsigned-payload`).
- `-T <file>` with `s3` sends `x-amz-content-sha256: UNSIGNED-PAYLOAD` of its own accord
  (`aws-sigv4-upload`): curl does not hash an upload it streams.
- `-T <file>` with `ec2` sends no content hash field and signs the empty body's hash while it
  sends the four bytes (`aws-sigv4-ec2-upload`); `-X PUT -d body` and `--data-binary @<file>`
  with `ec2` sign the body's own hash (`aws-sigv4-ec2-put`).

## Decision

1. **The contract.** `HttpAuthenticationVerdict` gains `BodyCheck`, an `IHttpRequestBodyCheck`
   (Surl.Protocol.Abstractions). A `Proceed` verdict carrying one lets the head in only
   provisionally: the HTTP server checks the upload limit (`413` as ever), reads the body - after
   `100 Continue` when it is expected (ADR-0027) - hashing its content bytes (the chunk data,
   not the chunked coding) with SHA-256, and asks `JudgeBodyAsync(bodySha256)`. Its verdict,
   with its login note, is answered as any verdict is, with the body already read: served, or a
   `401` that keeps the connection. A body that ends early or is malformed is `400`, and one the
   server cannot frame (another transfer coding) is `400` without being read; both are refusals.
   The session is never shown the body, only its hash.
2. **Why a check after the body, not a body read before judging.** Reading every body before
   judging would let any client make the server read up to the upload limit before a login is
   checked. With the check after, only a request whose head already passes what the head can
   show gets its body read: for `s3` the signature itself is checked on the head, and for every
   service a date outside the window is refused before the body.
3. **In the verifier.** `AwsSigV4AuthenticationMethod`:
   - With an `x-<provider>-content-sha256` field: the signature is checked on the head over the
     field as sent. Refused: refused now. Accepted with the value `UNSIGNED-PAYLOAD`: accepted,
     the body unbound, as AWS does. Accepted with any other value: the body must hash to it
     (hex, without regard to case) - a request with no body is compared with the empty body's
     hash at once, any other is `AwaitingBody`.
   - Without one: a request with no body is signed over the empty body's hash, as before; one
     with a body is `AwaitingBody`, and the signature is checked over the body's hash once it is
     read. An unknown key still costs the same one comparison, after the body.
   - `HttpCredentialOutcome.AwaitingBody` with `HttpCredentialCheck.CheckBody` carries the rest;
     `HttpAuthenticationSession` turns it into the `Proceed` verdict with the body check and no
     login note, and answers the body check's refusal after the refusal delay, with the note, as
     any refusal (ADR-0032 section 8).
4. **`curl -T` with a service other than `s3` is refused.** curl signs the empty body's hash and
   sends another body, so the body received is not the one signed; AWS answers it the same way.
   Accepting it would mean accepting any body under a signature of the empty one. curl exits 0
   with the `401` (no `-f`), as the conformance test pins; `--data-binary @<file>` with `-X PUT`
   signs the body and is served.

## Alternatives considered

- **Reading a bounded body before judging such a request.** Rejected for decision 2's reason,
  and because it would need a second bound beside the upload limit.
- **Leaving `UNSIGNED-PAYLOAD` bound or refused.** Rejected: it is AWS's documented way to sign
  without the body, curl sends it itself for `s3 -T`, and a mate for curl serves what curl signs.
- **Accepting a body under a signature over the empty hash**, so `curl -T` with `ec2` is served.
  Rejected: it unbinds every body, which is what this ADR exists to stop, and AWS refuses it.

## Consequences

- A body that is not the one signed is refused for every service, and `s3 -T` is served.
- The HTTP server reads a body before the login is final only for a verdict that asks it to;
  every other login is still judged before any body byte (ADR-0032 section 4).
