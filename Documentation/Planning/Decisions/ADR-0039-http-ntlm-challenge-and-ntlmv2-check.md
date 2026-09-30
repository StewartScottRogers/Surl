# ADR-0039 — HTTP NTLM: the challenge Surl sends and the NTLMv2 answer it checks

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-120.

## Context

[ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) section 3
makes NTLM a named choice (`--auth ntlm`), section 4 offers it as the bare challenge `NTLM` and
answers its type 2 step as a `401` continuation, section 6 keeps its handshake in the
per-connection session, and section 8 demands fixed-time checks and a dummy for an unknown
user. What the `CHALLENGE_MESSAGE` holds and which answers are checked was left to BL-120.

Measured on 2026-09-29 with `Record-CurlExchange.ps1 -ResponsesPerConnection 2` (added for this)
and the pinned reference build (curl 8.21.0, win-x64, SSPI, SHA-256 `0E773709…8778`), running
`-sS --ntlm -u tester:secret http://127.0.0.1:18120/x`; fixtures in
`Surl.Authentication.UnitTests/Fixtures/ntlm*`:

- curl sends its `NEGOTIATE_MESSAGE` on the first request, unasked, with flags `0xA2088207`
  (Unicode, OEM, request target, NTLM, always sign, extended session security, version,
  128-bit, 56-bit).
- Answered with the challenge below, it sends an NTLMv2 `AUTHENTICATE_MESSAGE` on the same
  connection: user `tester`, an empty domain, the workstation's name, an `NtChallengeResponse`
  far longer than NTLMv1's 24 bytes whose blob carries a timestamp and the target information
  echoed back, and a `Version` and MIC field. Answered `200`, curl exits 0 with the body.
- With a wrong password and the second leg answered `401` with the bare `NTLM`, curl gives up:
  exit 0 with an empty body, or 22 (`The requested URL returned error: 401`) with `-f`.

The Linux and macOS reference builds use curl's own NTLM, which offers OEM strings; they are
not measured here, only in the conformance test that proves the whole exchange.

## Decision

1. **The challenge.** A `NEGOTIATE_MESSAGE` is answered with a `401` carrying
   `WWW-Authenticate: NTLM <base64 CHALLENGE_MESSAGE>`, the message laid out as [MS-NLMP]
   section 2.2.1.2 says with no `Version` field (and so without `NTLMSSP_NEGOTIATE_VERSION`):
   - flags: request target, NTLM, always sign, target type server and target info, always;
     Unicode when the client offered it and OEM otherwise; extended session security, 128-bit
     and 56-bit only when the client asked;
   - target name `SURL`, in the negotiated character set;
   - target information: `MsvAvNbDomainName` and `MsvAvNbComputerName`, both `SURL` in
     UTF-16LE, then `MsvAvEOL`. No `MsvAvTimestamp`, since Surl verifies no MIC.
   The name is fixed, so it says nothing about the host (ADR-0006 section 3). Target
   information is what makes a client answer with NTLMv2.
2. **The server challenge** is eight bytes from `RandomNumberGenerator` per `NEGOTIATE_MESSAGE`,
   behind an injected source so the tests replay a recorded answer to a fixed one.
3. **One challenge per connection, used once.** The connection keeps the last challenge it
   issued. Any later leg uses it up, whatever it holds, so an `AUTHENTICATE_MESSAGE` is checked
   at most once, only on the connection that was challenged: replayed on the same connection or
   a new one, it is refused. A new `NEGOTIATE_MESSAGE` starts over.
4. **Only NTLMv2 is checked** ([MS-NLMP] section 3.3.2): `NTProofStr` is recomputed from the
   account's NT hash, the user and domain as sent, the server challenge and the client's blob,
   and compared in fixed time. A 24-byte (NTLMv1) response is refused; neither measured build
   sends one to a challenge carrying target information. The LM response, the MIC, the
   workstation and the session key are not read. The blob's timestamp is not checked either:
   the random, single-use server challenge is what stops a replay.
5. **Strings** in the `AUTHENTICATE_MESSAGE` are read as UTF-16LE when its own flags say
   Unicode, and one byte per character (Latin-1) otherwise, as curl's own NTLM writes them.
6. **Accounts.** `AccountBook` computes each named account's NT hash, `MD4(UTF-16LE(password))`
   with `Surl.Cryptography.Md4`, at start-up. The user name is matched exactly, like every other
   method's; an unknown name, and the empty one (the Bearer token's), gets a dummy with a random
   hash, so it costs the same work and is refused. NTOWFv2 upper-cases the user, so an answer
   computed for `TESTER` proves the same password as one for `tester`, but only the name the
   account was configured with is found.
7. **Outcomes.** A malformed message, a type 2, an answer on a connection with no challenge and a
   wrong proof are all `Refused` (delayed by the policy, ADR-0032 section 8) and never thrown; a
   refused or accepted answer names its user for the login note (ADR-0038).

## Alternatives considered

- **Echo every client flag.** Rejected: granting signing or sealing Surl does not do would claim
  a session security it never provides.
- **Send `MsvAvTimestamp` and verify the MIC.** Rejected for now: the MIC protects the three
  messages against tampering in a signed session, which HTTP NTLM never becomes; the
  single-use server challenge already binds the answer to this connection.
- **Accept NTLMv1.** Rejected: MD4 and DES of the password with no client nonce, and no measured
  client sends it.

## Consequences

- `NtlmAuthenticationMethod` is composed like every other method; `surl` offers it only with
  `--auth` naming `ntlm`.
- Once NTLM accepts, the connection is not remembered as logged in: a later request on it without
  an `Authorization` is challenged again (ADR-0032 section 4). curl sends no `Authorization` on
  a later request once NTLM has succeeded, so several URLs over one connection need that
  remembered; it is later work. *Amended by
  [ADR-0041](ADR-0041-an-accepted-ntlm-login-is-remembered-by-its-http-connection.md): the
  connection now remembers the accepted account.*
