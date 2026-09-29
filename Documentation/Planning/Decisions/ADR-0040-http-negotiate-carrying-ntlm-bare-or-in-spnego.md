# ADR-0040 — HTTP Negotiate carrying NTLM: bare or inside SPNEGO, and what the Windows reference build sent

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-121.

## Context

[ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) section 3
makes Negotiate a named choice (`--auth negotiate`), section 4 offers it as the bare challenge
`Negotiate` and answers each handshake step as a `401` continuation, section 6 lets an accepted
verdict carry a final `WWW-Authenticate` token, and section 11 says Negotiate carries NTLM now
and a Kerberos token inside it is refused like any other bad credential until Kerberos lands.
[ADR-0039](ADR-0039-http-ntlm-challenge-and-ntlmv2-check.md) decides the NTLM handshake itself.
What Negotiate wraps around it was left to BL-121.

RFC 4559 carries a GSS-API token after `Negotiate`. The token a client sends first is SPNEGO's
(RFC 4178) `InitialContextToken`: `[APPLICATION 0]`, the object identifier `1.3.6.1.5.5.2`, then
a `NegTokenInit` listing the mechanisms the client offers, most preferred first, with an
optional optimistic token for the first. Every later token of either side is a `negTokenResp`
(`negState`, `supportedMech`, `responseToken`, `mechListMIC`). Windows' Negotiate package also
sends bare NTLM (`NTLMSSP\0...`) after `Negotiate` when it falls back to NTLM.

### Measured

With `Record-CurlExchange.ps1 -ResponsesPerConnection 2` and the pinned Windows reference build
(curl 8.21.0, SSPI, SHA-256 `0E773709…8778`) on the lane machine (Windows 11 10.0.26200, not in
a domain), answering every request with `401` and `WWW-Authenticate: Negotiate`:

- `-sS -v --negotiate -u tester:secret http://127.0.0.1:18121/x`: SSPI's
  `InitializeSecurityContext` fails with `SEC_E_NO_CREDENTIALS` before the first request and
  again after the `401`; curl sends no `Authorization` at all and exits 0 with an empty body
  (22 with `-f`, `curl: (22) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS ...`).
  Fixture: `Surl.Authentication.UnitTests/Fixtures/negotiate-no-token`.
- The same with `localhost`, a `--resolve`d dotted name, the computer's name, `SURL\tester`,
  `tester@surl`, `.\tester`, `-u :` (the logged-in user) and `--delegation always`: the same
  failure, and nothing in the NTLM operational event log.
- A C# probe on the same machine making curl's exact SSPI calls (`AcquireCredentialsHandle` for
  `Negotiate` with the same identity, `InitializeSecurityContext` with `ISC_REQ_CONFIDENTIALITY`
  and the SPN `HTTP/127.0.0.1`, ANSI and Unicode) gets `SEC_I_CONTINUE_NEEDED` and a bare
  40-byte `NTLMSSP` `NEGOTIATE_MESSAGE`. Why curl's process gets `SEC_E_NO_CREDENTIALS` where
  the probe does not is not known yet.

So no pinned upstream build sent a SPNEGO token here: the Linux and macOS builds list no
`SPNEGO` (`UpstreamCurlBuilds.json`), and the Windows build sent none on this machine.

## Decision

1. **The challenge** is the bare `Negotiate` (ADR-0032 section 4).
2. **Bare NTLM inside Negotiate.** A token that is an NTLM message is handed to the connection's
   NTLM handshake (ADR-0039) and its `CHALLENGE_MESSAGE` is sent back bare, as
   `WWW-Authenticate: Negotiate <base64>`, the way Windows' Negotiate package expects when it
   has fallen back to NTLM. An accepted answer carries no final token.
3. **SPNEGO.** A token starting `[APPLICATION 0]` is read as an `InitialContextToken` with
   `System.Formats.Asn1` under DER rules; one naming another mechanism than SPNEGO (a bare
   Kerberos token) is refused.
   - **NTLM is the only mechanism Surl selects.** A `NegTokenInit` whose `mechTypes` do not
     include NTLMSSP (`1.3.6.1.4.1.311.2.2.10`) is refused, as ADR-0032 section 11 says.
   - **NTLM first, with a token:** the optimistic token is NTLM's `NEGOTIATE_MESSAGE`, answered
     at once with `negTokenResp { negState accept-incomplete, supportedMech NTLMSSP,
     responseToken CHALLENGE_MESSAGE }`.
   - **Another mechanism first (Kerberos), or no token:** the optimistic token belongs to that
     mechanism and is not read; the reply is `negTokenResp { negState accept-incomplete,
     supportedMech NTLMSSP }` (RFC 4178 section 3.2), and the client starts NTLM in its next
     `negTokenResp`.
   - **Later tokens** are accepted as `negTokenResp` only on a connection whose last reply was
     one; their `responseToken` goes to the NTLM handshake. A `CHALLENGE_MESSAGE` goes back as
     `negTokenResp { negState accept-incomplete, responseToken }` (no `supportedMech`, which is
     sent in the first reply only). An accepted `AUTHENTICATE_MESSAGE` is served with the final
     `WWW-Authenticate: Negotiate oQcwBaADCgEA`, `negTokenResp { negState accept-completed }`.
4. **No `mechListMIC`.** The client's `mechListMIC` is skipped, and Surl sends none. RFC 4178
   section 5 uses no MIC when the selected mechanism gives no integrity protection, and the
   `CHALLENGE_MESSAGE` of ADR-0039 grants neither signing nor sealing, so the NTLM context
   gives none. The random, single-use server challenge is what binds the answer to the
   connection, as for bare NTLM.
5. **One handshake per connection, one challenge used once.** The NTLM handshake is shared code
   (`NtlmHandshake`) under both `NTLM` and `Negotiate`; any token Negotiate refuses before it
   reaches the handshake still uses the pending challenge up.
6. **Refusals are never exceptions.** Malformed DER, trailing bytes, a missing field, a
   `negTokenResp` with no `responseToken` or out of turn, and a token that is neither NTLM nor
   SPNEGO are all `Refused`, delayed by the policy (ADR-0032 section 8); an answer whose
   `AUTHENTICATE_MESSAGE` was read names its user for the login note (ADR-0038).
7. **The tests** replay the NTLM messages the Windows reference build sent for BL-120, bare and
   wrapped in SPNEGO as RFC 4178 lays it out, and pin the server's `negTokenResp` bytes, written
   out by hand from the RFC, for the recorded fixed challenge. They also pin what the reference
   build sent when offered `Negotiate` here (nothing). Proving the whole exchange against
   `surl` needs Negotiate composed in `Surl.Console`, as NTLM's does (BL-132), and a machine
   where the reference build's SSPI makes a token; both are the follow-up task BL-134.

## Alternatives considered

- **Send `negState request-mic` and check the client's MIC.** Rejected for now: it needs NTLM
  signing (the exported session key, the client-to-server signing key and the MAC of
  [MS-NLMP] section 3.4.4) over a context that grants no signing, and no measured client has
  sent a MIC to check it against. If the reference build is ever measured sending one and
  refusing a reply without the server's, this is revisited.
- **Refuse bare NTLM inside Negotiate.** Rejected: it is what Windows' Negotiate package itself
  produced on this machine, and servers that speak Negotiate to Windows clients accept it.
- **Answer a Kerberos-only `NegTokenInit` with `negState reject`.** Rejected: ADR-0032 section
  11 refuses it like any other bad credential, a `401` with every challenge after the refusal
  delay, and a `reject` token would need a verdict the policy's refusal does not carry.

## Consequences

- `NegotiateAuthenticationMethod` is composed like every other method; `surl` offers it only
  once `Surl.Console` composes it (BL-134) and `--auth` names `negotiate`.
- Kerberos inside Negotiate is later work, built by hand (ADR-0032 section 11).
- Like NTLM (ADR-0039, "Consequences"), an accepted Negotiate login is not remembered by the
  connection yet.
