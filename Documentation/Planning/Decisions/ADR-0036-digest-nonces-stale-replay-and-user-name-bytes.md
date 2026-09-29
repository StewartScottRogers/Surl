# ADR-0036 — Digest nonces, `stale=true`, replayed answers and user-name bytes

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-113.

## Context

[ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) section 4
fixes the three Digest challenges (`Digest realm="surl", qop="auth", algorithm=<A>,
nonce="<nonce>"` for `MD5`, `SHA-256`, `SHA-512-256`, in that order) and leaves the nonce, its
lifetime and `stale=true` to BL-113. It keeps account passwords only as SHA-256 hashes
(section 8), which Digest cannot use: every Digest response is built on
`H(username ":" realm ":" password)`.

Measured on 2026-09-29 with `Record-CurlExchange.ps1 -Connections 2` and the pinned reference
build (curl 8.21.0, win-x64, SSPI, SHA-256 `0E773709…8778`), connection 1 answering a `401`
with the MD5 challenge above and the fixed nonce `fixturenonce`; fixtures in
`Surl.Authentication.UnitTests/Fixtures/digest-*`:

- `--digest -u tester:secret` answered `algorithm=MD5`, `qop="auth"`, `uri="/x"`, a 32-digit
  `cnonce` and, on this run, `nc=00000002`; an `MD5-sess` challenge was answered with
  `algorithm=MD5-sess`. A query (`/dir/x?y=1&z=%41`) is sent in `uri` exactly as in the request
  line; `-X POST -d x` sends the method `POST` in the hash.
- `-u tëster:secret` on the Windows command line sent `username="t\xEBster"`, the Windows-1252
  byte `EB`, and the response matches `H` over the ISO-8859-1 bytes of
  `tëster:surl:secret`, not the UTF-8 ones.
- The same user in a UTF-8 config file (`-K`) made the build fail with exit 94 before sending
  an answer: SSPI refuses it. The Linux and macOS builds use curl's own Digest code and a UTF-8
  command line; BL-118 measures them.
- The challenge carries no `charset` parameter, so RFC 7616 section 4 leaves the encoding to
  the legacy convention, which in practice is ISO-8859-1.

## Decision

1. **Nonces are stateless and signed.** A nonce is 8 bytes of issue time (the injected
   `TimeProvider`'s UTC ticks, big-endian), 16 bytes from `RandomNumberGenerator`, and the
   first 16 bytes of HMAC-SHA-256 over those 24 under a 32-byte key drawn from
   `RandomNumberGenerator` when the method is built; written as 80 lower-case hex digits. An
   answer's nonce is recognised by recomputing the HMAC and comparing with
   `CryptographicOperations.FixedTimeEquals`; any other nonce, including one of ours written in
   upper case, is refused, so each nonce has one spelling and one `nc` record. One nonce serves all
   three challenges of one `401`. Nothing is stored per nonce issued, so a flood of `401`s
   costs no memory.
2. **Lifetime: five minutes.** A nonce older than that is expired. Apache's `mod_auth_digest`
   uses the same default; it is long enough for a slow upload's `100 Continue` round and short
   enough to bound a captured answer's use.
3. **`stale=true`.** An answer whose nonce is expired but which is otherwise right (the
   response matches an account) gets a `401` with the three Digest challenges on a new nonce,
   each followed by `, stale=true` (RFC 7616 section 3.3), and no other method's challenge.
   It is not delayed: no wrong secret was sent (ADR-0032 section 8's delay is for refusals).
   A wrong answer on an expired nonce is refused like any other.
4. **Replay.** A verified answer's `nc` is remembered per nonce, until the nonce expires, and an
   answer whose `nc` is not higher than one already accepted with that nonce is refused.
   Only nonces that verified answers used are remembered, so memory is bounded by logins in
   the last five minutes. `nc` need not start at 1 (the reference build sent 2). A nonce that
   expires between the freshness check and the record is refused, so the purge of its count
   never lets a replay through.
5. **What an answer must hold.** `username`, `realm`, `nonce`, `uri`, `response`, `cnonce`,
   `nc` (eight hex digits) and `qop=auth`; `realm` is `surl`; `algorithm` is absent (MD5) or one
   of `MD5`, `SHA-256`, `SHA-512-256` and their `-sess` forms, matched case-insensitively. The
   `-sess` forms are accepted though not offered: RFC 7616 lets a client use them, they cost
   nothing more to check, and they need the password all the same. `userhash=true` is refused,
   since `userhash` is never offered. `uri` must equal the request target exactly. The response
   is compared, lower-cased, as bytes with `FixedTimeEquals`. Any other answer is refused, never
   thrown.
6. **User-name bytes.** The `username` value is read as the bytes sent. It names an account
   when it is the UTF-8 encoding of the account's name, and the hashes then cover the UTF-8
   bytes of `name:surl:password`; or, for an account whose name and password are all
   ISO-8859-1, when it is the ISO-8859-1 encoding, and the hashes then cover the ISO-8859-1
   bytes. A UTF-8 spelling always wins over another account's ISO-8859-1 spelling. An ASCII
   name is its own ISO-8859-1 spelling, so its entry carries both sets of hashes and a
   non-ASCII password matches in either encoding. Every lookup checks exactly two sets (the
   second random when there is no ISO-8859-1 set), so the work never depends on the account.
   So the Windows reference build's command line logs in with Digest, where Basic's announced
   `charset="UTF-8"` makes ADR-0035 refuse it. The Windows-1252 characters outside
   ISO-8859-1 (bytes `80`-`9F`, `€` among them) are not mapped: such a name or password logs
   in from a UTF-8 client only.
7. **Account storage.** `AccountBook` computes, at start-up, the three user hashes
   (`H(name:surl:password)` under MD5, SHA-256 and SHA-512/256) for each encoding of each named
   account, and still keeps no clear password. An unknown name is checked against a dummy with
   random hashes, costing one comparison like a wrong password (ADR-0032 section 8).

## Alternatives considered

- **Keeping issued nonces in a table.** Needs memory per `401` and a purge; a signed nonce
  proves itself.
- **UTF-8 only, as ADR-0035 does for Basic.** Basic announces `charset="UTF-8"`; these
  challenges announce none, and adding `charset="UTF-8"` would change ADR-0032's pinned values.
  Refusing the measured Windows answer would leave every non-ASCII user of the reference build
  without a Digest login.
- **Keeping the clear password for Digest.** The user hashes are password-equivalent for Digest
  anyway, and hashing once at start-up keeps the clear text out of memory.

## Consequences

- `Surl.Authentication` references `Surl.Cryptography` (ADR-0032 section 7) for SHA-512/256.
- Nonces do not survive a restart: an answer to a nonce from before is refused and gets fresh
  challenges.
- `qop=auth-int` stays unoffered and is refused (ADR-0032 section 4).
