# ADR-0064 — Kerberos inside Negotiate: the choices ADR-0057 decision 8 left open

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-241 (FR-046).
- **Amends:** [ADR-0057](ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md)
  decision 8 (it spells out what decision 8 did not say); [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  section 1 (`--allow-anonymous` still reads a Negotiate token once `--keytab` is given).

## Context

BL-241 built ADR-0057 decision 8: `NegotiateAuthenticationMethod` accepts a Kerberos AP-REQ, bare
or inside SPNEGO, once `--keytab` gave a `KerberosAcceptor`. Three questions came up that
decision 8 does not answer. No pinned upstream curl build sends a Kerberos token on the lane
machine (ADR-0057, "What upstream curl 8.21.0 does (measured)"), so each is answered from
RFC 4178, RFC 4559 and ADR-0057's own reasoning; BL-242 is the end-to-end proof.

## Decision

1. **Kerberos selected but not first is refused.** The mechanism selected is the first of the
   client's `mechTypes` that surl supports (Kerberos under either OID, or NTLM). When that is
   Kerberos but another mechanism is listed before it (say NEGOEX,
   `1.3.6.1.4.1.311.2.2.30`), the optimistic token belongs to that other mechanism, so no AP-REQ
   arrived; answering would take a second leg and, by RFC 4178 section 5, a `mechListMIC`
   exchange. Decision 8 made Kerberos one leg, so this `NegTokenInit` is refused like one naming
   Kerberos first with no optimistic token: `401` with every challenge after the delay, with no
   user and no fallback to NTLM. Windows and MIT clients list Kerberos first.
2. **A `NegTokenInit` naming NTLM before Kerberos runs NTLM** exactly as ADR-0040 decides; with no
   supported mechanism at all it is refused, as before.
3. **`--allow-anonymous` over HTTP.** ADR-0032 serves every request unchecked under
   `--allow-anonymous` without reading `Authorization`. With `--keytab` given and `negotiate`
   accepted, a request carrying `Authorization: Negotiate` is now read: a token that selects
   Kerberos must decrypt under the keytab, as ADR-0057 decision 9 decides for SASL `GSSAPI`,
   because the final token (the AP-REP, the `mechListMIC`) needs the ticket's session key; only
   the account match of decision 10 is skipped. Such a login is served unchecked, with the final
   token and with no login note (the new `HttpCredentialOutcome.AcceptedUnchecked`); a ticket that
   does not decrypt is refused after the delay. Every other token - NTLM, bare or in SPNEGO, a
   `negTokenResp`, a `NegTokenInit` selecting nothing - is served unchecked as before. Without
   `--keytab`, `--allow-anonymous` reads no `Authorization` at all, as ADR-0032 decides. Upstream
   curl sends a Negotiate token only after a `401`, which `--allow-anonymous` never sends, so this
   touches only a client that sends one unasked.
4. **The composition.** `NegotiateAuthenticationMethod` gains the public constructor
   `(AccountBook, KerberosAcceptor?, bool allowAnonymous)`, which `Surl.Console` calls with
   `--keytab`'s acceptor and `--allow-anonymous`; the one-argument constructor stays and carries
   NTLM only.

## Consequences

- A client that lists another mechanism before Kerberos gets a `401` rather than a second leg. If
  BL-242's end-to-end proof shows a real client doing so, a later ADR adds the second leg with its
  `mechListMIC`.
- A Kerberos refusal's reason (`ticket expired`, `clock skew`) is not written to the verbose log
  for HTTP yet, as for SASL `GSSAPI` (BL-260).

## Alternatives considered

- **Serve every Negotiate token unchecked under `--allow-anonymous`,** as every other method is.
  Rejected: a client that asked for mutual authentication gets no AP-REP and cannot finish its
  context, and the SASL rule (decision 9) would differ from HTTP's for no reason.
- **A second leg for Kerberos when it is not first.** Rejected for now: decision 8 made Kerberos
  one leg, no measured client needs it, and it would bring the `mechListMIC` requirement of
  RFC 4178 section 5 with it.
