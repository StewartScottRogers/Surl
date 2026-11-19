# ADR-0178 — A raw NTLM LDAP bind needs `--auth ntlm`; `--auth negotiate` accepts only Negotiate binds

- **Status:** Accepted
- **Date:** 2026-11-19
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-19, in
  BL-1390. No package, no threshold change, no download.
- **Amends:** [ADR-0072](ADR-0072-how-the-ldap-server-answers-upstream-curl-and-what-directory-it-serves.md) decision 4 (the NTLM and `GSS-SPNEGO` binds; behaviour
  unchanged, only what `--aihelp ldap` says about it).

## Context

Finding 2 of the Surly cookbook bug report of 2026-11-18: `surl --aihelp ldap` said NTLM binds are "accepted with
`--auth ntlm` or `--auth negotiate`", yet a `curl --ntlm` bind is refused under `--auth negotiate`.

What the server does today, from the code:

- WinLDAP's `--ntlm` bind is a Sicily bind (`[9]` discovery, `[10]` NTLM `NEGOTIATE_MESSAGE`, `[11]`
  `AUTHENTICATE_MESSAGE`). `LdapSaslBindJudge` runs it as the SASL mechanism `NTLM`, whose method is `Ntlm`;
  `AuthenticationPolicy` offers and starts it only when `--auth` accepts `ntlm`, else `RefusedMechanism`, which
  the server answers `authMethodNotSupported` (7).
- WinLDAP's `--negotiate` bind is SASL `GSS-SPNEGO`, accepted when `--auth` accepts `negotiate` and run by
  `GssSpnegoSaslExchange`, which answers a bare NTLM token with the NTLM exchange.
- Consistency with HTTP: `HttpAuthenticationSession` treats an `Authorization` of a method `--auth` does not
  accept as missing. A raw `Authorization: NTLM` under `--auth negotiate` is therefore not accepted; only
  `Authorization: Negotiate`, bare NTLM token or SPNEGO-wrapped, is (`NegotiateConnectionVerifier`, ADR-0040).

The recorded bytes are the fixtures `Protocols/Messaging/Surl.Protocol.Ldap.UnitTests` replays
(`ldap-ntlm-sealed`, `ldap-negotiate-sealed`, measured from the pinned Windows upstream curl build, see
`Foundation/Surl.Authentication.UnitTests/Fixtures/README.md`): `--ntlm` sends the Sicily binds, `--negotiate`
sends `GSS-SPNEGO` carrying a bare NTLM message. This task did not re-record them.

## Decision

Keep the behaviour; correct the help. A raw NTLM bind (`curl --ntlm`) needs `--auth ntlm`. A Negotiate bind
(`curl --negotiate`), which carries NTLM inside `GSS-SPNEGO`, needs `--auth negotiate`. Reasons:

1. It matches HTTP, where each scheme is accepted only under its own `--auth` word.
2. Secure by default: accepting NTLM under `negotiate` would widen what a peer may do without being asked.
3. Nothing is lost: the Negotiate bind already reaches NTLM, and `--auth ntlm,negotiate` accepts both.

A refused raw NTLM bind is `authMethodNotSupported` (7), pinned by
`LdapProtocolServerSaslBindTests.ServeAsync_SicilyNtlmBindWhenThePolicyRefusesNtlm_IsAuthMethodNotSupported`.

## Consequences

- `AiHelpProse`'s `ldap` paragraph says which `--auth` word each bind needs.
- No production code changes. The real policy's refusal under `--auth negotiate` alone stays covered by the
  existing `Surl.Authentication` tests; the LDAP test pins how the server answers it.
