---
id: BL-1214
title: Decide how TLS-SRP and SOCKS5 GSS-API logins publish LoginJudgedEvent and honour refuse-login
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-060
created: 2026-11-15
completed: 2026-11-15
---
# BL-1214 — Decide how TLS-SRP and SOCKS5 GSS-API logins publish LoginJudgedEvent and honour refuse-login

## Goal

An ADR, decided by Claude under Stewart's delegation, says whether and how the two logins judged outside `AuthenticationPolicy` (TLS-SRP and SOCKS5 GSS-API) publish `LoginJudgedEvent` and honour a `refuse-login` rule. The follow-up tasks are filed.

## Context

- Found by the stand-in audit of 2026-11-15. ADR-0124 decision 1 says every judged login is published, and decision 4 says `refuse-login` acts at `FaultStage.Login` in `AuthenticationPolicy` (ADR-0128). Two logins bypass the policy:
  - **TLS-SRP** (ADR-0122): `Cryptography/Surl.TlsSrp.UnitLibrary/TlsSrpServer.cs` judges the user during the handshake against `TlsSrpAccounts` and only logs `The TLS-SRP login failed: ...` (lines 165-166). No event is published and no fault is consulted. ADR-0124 decision 4 also says "No fault alters the handshake itself", which a TLS-SRP `refuse-login` would do.
  - **SOCKS5 GSS-API** (BL-1066): `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/Socks5GssApiNegotiation.cs` line 51 calls `KerberosAcceptor.Accept` directly. No event, no fault.
- Questions:
  1. Does each publish `LoginJudgedEvent`? If so, with which scheme, account (SRP user name; Kerberos client principal) and mechanism, and from where? `Surl.TlsSrp` and `Surl.Networking` do not reference `Surl.Authentication`. The SOCKS server reaches the policy through `IAuthenticationPolicy`, which has no Kerberos-login member.
  2. Does `refuse-login` apply, given the handshake rule? If so, how is the refusal shaped: as a wrong password is (`bad_record_mac` at `Finished` for SRP; GSS-API failure for SOCKS)? If not, why not.
  3. Does `IAuthenticationPolicy` (in `Surl.Protocol.Abstractions`) need a new member? If so, that contract change gets its own task, which the server tasks depend on.
- Upstream: any byte Surl would send differently must be measured from pinned upstream curl 8.21.0 with `Record-CurlExchange.ps1` (`--tlsuser`/`--tlspassword`; `--socks5-gssapi`) before it is pinned. State the version in the ADR.

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/`, with the next free number, marked "Decided by Claude under Stewart's delegation", answers questions 1 to 3 for both logins and amends ADR-0124 (header "Amended by").
- [x] Every wire behaviour the ADR pins cites a measurement of pinned upstream curl 8.21.0, or states that no byte changes.
- [x] One feature task per library the ADR changes is filed with `task-board.ps1 new`, depending on this task, with exact `touches`. Contract tasks come before server tasks. Each is listed in Notes, or Notes says why none is needed.

## Notes

ADR-0165 decides it. Follow-up tasks filed: BL-1221 (Abstractions contracts), BL-1222 (TlsSrp), BL-1223 (Networking), BL-1224 (Core engine), BL-1225 (Authentication), BL-1226 (Socks). Choice recorded: TLS-SRP judged by an engine-side ITlsLoginJudge because TlsSrp and Networking hold no publisher; Kerberos through a new default IAuthenticationPolicy member. No wire byte changes, so no new curl measurement.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. ADR-0165 written; BL-1221 to BL-1226 filed
