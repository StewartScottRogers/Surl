# ADR-0043 — HTTP AWS Signature Version 4: what is signed, which secret, and the request-time window

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-122.
- **Builds on:** [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  sections 1, 3, 4, 6 and 8.

## Context

ADR-0032 accepts `aws-sigv4` by default, offers no challenge for it (curl signs the first
request unasked), takes an account's name as the access key ID and its password as the secret
access key, and leaves the check itself - and its request-time window - to BL-122.

### Measured

Pinned upstream curl 8.21.0 (win-x64 reference build), `Record-CurlExchange.ps1 -Port 18122`,
`-u AKIDEXAMPLE:secret`; the fixtures are `aws-sigv4-*` in
`Surl.Authentication.UnitTests/Fixtures` (its README lists every command line).

- `--aws-sigv4 aws:amz:us-east-1:s3` sends `Authorization: AWS4-HMAC-SHA256
  Credential=AKIDEXAMPLE/20260929/us-east-1/s3/aws4_request,
  SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=<64 lower-case hex>`, with
  `X-Amz-Date: 20260929T210307Z` and `x-amz-content-sha256: <SHA-256 of the body>`.
- For any other service (`ec2`) it sends no content hash field and signs `host;x-amz-date`; the
  payload hash in the canonical request is then the SHA-256 of the body itself.
- `--aws-sigv4 aws` alone takes the service and region from the host name and names the fields
  after the provider: `X-Aws-Date`, `x-aws-content-sha256`. `osc:osc:eu-west-2:api` sends the
  scheme `OSC4-HMAC-SHA256`, the terminator `osc4_request` and `X-Osc-Date`.
- Every `-H` field is signed too (`content-type;host;...;x-test`), its value trimmed and each
  inner run of spaces made one.
- The canonical query (found by replaying the recorded signature against candidate canonical
  forms): each parameter split at its first `=`, `+` read as a space, `%XX` read as its byte,
  then every byte but `A-Z a-z 0-9 - . _ ~` percent-encoded in upper-case hex (so `%7e` is `~`,
  `%zz` is `%25zz`, a second `=` is `%3D`, a parameter with no `=` gains one), sorted by name and
  then by value. The path is used as sent for `s3` and percent-encoded again (`%41` becomes
  `%2541`) for every other service.
- With a wrong secret and a `401` carrying a Digest challenge, curl exits 0 with an empty body
  and does not sign again.

## Decision

1. **The check.** `AwsSigV4AuthenticationMethod` rebuilds the canonical request from the request
   as received - the method, the path and query from the target (origin or absolute form), each
   signed field (`name:value`, several joined with `,`), the signed header list and the payload
   hash - hashes it into the string to sign (`<PROVIDER>4-HMAC-SHA256`, the date field, the
   scope, the hash), derives the signing key from `<PROVIDER>4` + secret through the scope's
   date, region, service and terminator, and compares the HMAC-SHA256 with the signature sent
   in fixed time (ADR-0032 section 8). The request's bytes are hashed one byte per character,
   as the HTTP server reads them.
2. **Any provider, any region, any service.** Every scheme of ASCII letters and digits before
   `4-HMAC-SHA256` names the method; the provider is read from the scope's terminator and the
   date field is the signed `x-<provider>-date`. Region and service are not configured: they are
   bound into the signature, and Surl answers whatever curl was told to sign for. The login note
   names the method `AWS4-HMAC-SHA256` whatever the provider, and the user as the access key ID.
3. **The secret is the password, as UTF-8.** `AccountBook` keeps each named account's password
   as UTF-8 bytes for this method (signing keys derive from the secret itself, so no hash of it
   will do; ADR-0032 section 2 already keeps passwords in clear for this reason). An unknown
   access key, the empty one included, is checked against a random secret, so it costs the same
   one comparison and answers the same.
4. **The window is 15 minutes either way**, inclusive, between the signed date field and the
   injected `TimeProvider`'s time: AWS's own allowance for clock skew, so a client that works
   against AWS works against Surl. The scope's date must be the date field's day. A signature
   may be replayed inside the window, as against AWS; nothing per-signature is remembered.
5. **What must be signed.** `host` and the date field must be in `SignedHeaders`; a signed field
   missing from the request, malformed credentials, a signature not 64 lower-case hex digits and
   a target with no path are refusals, never exceptions.
6. **The body.** The policy judges the head alone (ADR-0032 section 6). The payload hash is the
   `x-<provider>-content-sha256` field when sent (always, for `s3`); without one, a request with
   no body (no `Transfer-Encoding`, no `Content-Length` or `0`) uses the empty body's hash and any
   other request is refused. So today a signed `s3` write is accepted without its body being
   compared with the signed hash, and a write for another service is refused. Binding the body
   to the signature is BL-136. *Amended by
   [ADR-0045](ADR-0045-an-aws-signature-version-4-body-is-bound-after-it-is-read.md): the body
   is now bound once it is read, and a write for any service is checked over it.*

## Alternatives considered

- **Configuring a region and service** (`--aws-region`, say). Rejected: a mate for upstream curl
  answers what curl signs; the signature already binds both, so a fixed value would only refuse
  requests that verify.
- **Only `AWS4-HMAC-SHA256`.** Rejected: curl's `--aws-sigv4` takes any provider, and the
  arithmetic is the same.
- **A window of 5 minutes, or none.** Five would refuse clients AWS accepts; none would make a
  captured request good forever.
- **Reading the body before judging.** Left to BL-136, since it changes where the HTTP server
  judges a login (ADR-0032 section 4).

## Consequences

- `surl --auth aws-sigv4` (and the default set) now checks curl's `--aws-sigv4`: every `--auth`
  word names an implemented method, so `Surl.Console` no longer refuses `aws-sigv4` with 2.
- With `--auth aws-sigv4` alone, a request without a signature gets `403`, since no challenge
  can be offered (ADR-0032 section 4, step 5).
