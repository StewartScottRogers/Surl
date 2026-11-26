# ADR-0043 — HTTP AWS Signature Version 4: what is signed, which secret, and the request-time window

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-122.
- **Builds on:** [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  sections 1, 3, 4, 6 and 8.
- **Amended by:** amendment 1 below (2026-11-21, decided by Claude under Stewart's delegation;
  built by BL-1548): over HTTP/2 and HTTP/3 the signed `host` comes from `:authority`; amendment 2
  below (2026-11-25, decided by Claude under Stewart's delegation; built by BL-1719): a
  `SignedHeaders` list naming a header twice is malformed.

## Context

ADR-0032 accepts `aws-sigv4` by default, offers no challenge for it (curl signs the first
request unasked), takes an account's name as the access key ID and its password as the secret
access key, and leaves the check itself - and its request-time window - to BL-122.

### Measured

Pinned upstream curl 8.21.0 (win-x64 reference build), `Record-CurlExchange.ps1 -Port 18122`,
`-u AKIDEXAMPLE:secret`; the fixtures are `aws-sigv4-*` in
`Foundation/Surl.Authentication.UnitTests/Fixtures` (its README lists every command line).

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

## Amendment 1 (2026-11-21) — HTTP/2 and HTTP/3 sign `host` from `:authority`

Decided by Claude under Stewart's delegation. Built by BL-1548; recorded first by BL-1547.

### Problem

Section 5 requires `host` in `SignedHeaders`, and section 1 rebuilds each signed field from the
request's fields as received. Over HTTP/2 and HTTP/3 there is no `host` field to find: the HTTP
server's request head keeps `:authority` apart from the regular fields, so
`AwsSigV4CanonicalRequest.BuildHeaders` finds no `host`, returns `null`, and every HTTP/2 and
HTTP/3 Signature Version 4 login is refused.

### Evidence (upstream curl 8.21.0, tag `curl-8_21_0`, and the RFCs)

- `lib/http_aws_sigv4.c`, `make_headers`: when the user set no `Host` header, the signed header
  list starts with the `Host: <host>[:port]` line curl builds from the URL
  (`data->state.aptr.host`, cut at the CR), else `host:<hostname>`. `host` is signed whatever the
  HTTP version.
- `lib/http.c`, `Curl_http_req_to_h2`: takes `:authority` from the request's `Host` header; and
  `h2_permissible_field` (the `H2_NON_FIELD` list) drops `Host` from the HTTP/2 and HTTP/3 field
  list. The server therefore receives `:authority: 127.0.0.1:8443` and no `host` field, while the
  signature covers `host:127.0.0.1:8443`.
- RFC 9113 section 8.3.1 (and RFC 9114 section 4.3.1): `:authority` carries what `Host` carries
  in HTTP/1.1, and a gateway making an HTTP/1.1 request from it builds `Host` from `:authority`.

### Decision

1. **Over HTTP/2 and HTTP/3, when the request has no `host` field, the HTTP server hands the login
   check a `host` field whose value is `:authority` as received** (no case change, no port added
   or removed), so the canonical request holds the same `host:<value>` curl signed.
2. **A request that also carries a `host` field is judged on the fields as received.** Nothing is
   replaced, so a client that sends both is held to what it sent.
3. **The change lives in the HTTP server (`HttpRequestAnswerer`, where the login request is
   built), not in `Surl.Authentication` and not in the shared `HttpAuthenticationRequest`
   contract.** The S3 and Cloud Storage servers answer HTTP/1.1 only (ADR-0108 decision 5,
   ADR-0110), where `host` is already a field, so only the HTTP server has an `:authority` to
   map; the authentication library stays ignorant of HTTP versions and the contract does not grow.

### Consequences

- A test using `https://` with `--aws-sigv4` gets the same answer over HTTP/1.1, HTTP/2 and
  HTTP/3. No store, event or fault changes.

## Amendment 2 (2026-11-25) — a `SignedHeaders` list names each header once

Decided by Claude under Stewart's delegation. Built by BL-1719.

### Problem

Section 1 writes, for each name in the signed header list, every value of the fields with that
name. A list repeating a name wrote those values once per repetition. Under the default
`--max-request-head` (102400 bytes), one field of about 50 000 bytes named about 25 000 times
asked for over a billion characters. The canonical request is built for an unknown access key
too (section 3), so no account was needed.

### Evidence

- Pinned upstream curl 8.21.0 (win-x64 reference build), `Record-CurlExchange.ps1 -Port 18719`,
  `--aws-sigv4 aws:amz:us-east-1:s3 -u AKIDEXAMPLE:secret -H 'X-Test: b' -H 'x-test:  a  '
  -H 'X-TEST: c'` (fixture `aws-sigv4-repeated-header`): curl sends all three fields and
  `SignedHeaders=host;x-amz-content-sha256;x-amz-date;x-test`, the name listed once, and the
  signature verifies under section 1 (the three values joined with `,` in the order sent).
- AWS's "Create a canonical request" lists each signed header name once, lower-case and sorted.

### Decision

1. **A signed header list naming a header twice, compared ignoring case, is refused
   `MalformedCredentials`**, before any canonical request is built. Case is ignored because
   section 1 matches fields to names that way, so `host;Host` repeats a field's values too.
2. **The rule is in the credentials' reader** (`AwsSigV4Authorization.TryCreate`), so it covers
   the `Authorization` form, the presigned form (`X-Amz-SignedHeaders`) and
   `GOOG4-HMAC-SHA256`.
3. **Nothing else about the list is newly required.** Order and letter case are not checked: a
   list out of order or in upper case is still judged by its signature.

### Consequences

- The canonical request holds each field's value at most once, so its size grows no faster
  than the request head.
- Like other credentials that do not read, the refusal names no user.
