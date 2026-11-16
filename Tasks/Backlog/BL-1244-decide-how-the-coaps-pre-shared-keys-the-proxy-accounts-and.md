---
id: BL-1244
title: Decide how the coaps pre-shared keys, the proxy accounts and the TLS-SRP accounts are judged, published and controlled like the account store
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-061
created: 2026-11-16
completed:
---
# BL-1244 — Decide how the coaps pre-shared keys, the proxy accounts and the TLS-SRP accounts are judged, published and controlled like the account store

## Goal

An ADR, decided by Claude under Stewart's delegation, says for each of the three account sources that live outside the instance's `AccountBook` whether a test can add, remove, reset, snapshot and restore them, and how a `coaps` pre-shared-key login publishes `LoginJudgedEvent` and honours `refuse-login`. The follow-up tasks are filed.

## Context

Found by the second stand-in audit (2026-11-16). ADR-0124 decision 3 makes the accounts controllable (`ComposedStores` `AddAccount`, `RemoveAccount`, reset, snapshot, restore; `Surl.Api` `Accounts`, `Surl.Mcp` `add_account`/`remove_account`, `POST/DELETE /v1/accounts`), decision 1 publishes every judged login, and decision 4 refuses a login on a `refuse-login` rule (ADR-0128). Three account sources are outside all of that:

1. **`coaps` pre-shared keys.** `Application/Surl.Composition.UnitLibrary/DeferredCoapsProtocolServer.cs` builds `CoapDtlsCredentials` once, on the first flow, from `ComposedProtocolServices.PreSharedKeyAccounts` (`AuthenticationComposition.ReadPreSharedKeyAccounts`: `--user` and `--user-file` read at start). Its `LookUpKey` hands `Surl.Dtls` the password bytes for a PSK identity. So an account added or removed at run time, or by reset/restore, never changes who may log in over `coaps`; no `LoginJudgedEvent` is published for a PSK handshake; and `refuse-login;account=alice` does not refuse one. The `coaps` command-stage fault point (`CoapRequestFaults.cs`) also has no `Account`.
2. **Proxy accounts** (`-U`, `--proxy-user-file`, ADR-0088 decision 5). `AuthenticationComposition.ComposeProxyPolicy` builds a separate `AuthenticationPolicy` over its own account book, which `ComposedStores` does not hold, so the HTTP proxy's and SOCKS's accounts cannot be seeded, removed or reset by a test. (Their logins are already published and faultable through the policy.)
3. **TLS-SRP accounts** (`--tlsuser`, `--srp-verifiers`, ADR-0122). `ComposedTlsSrpAccounts` is read once at start; ADR-0165 publishes and faults their logins, but the accounts cannot be changed at run time.

ADR-0124 decision 3 and ADR-0132 name only the `--user`, `--user-file`, `--mailbox` and group accounts and do not say these three stay out. Questions the ADR answers:

- For each source: in or out of store control, and why (secure by default: an account a test adds is a loosening only when asked for). If in, which `StoreKind` and which host surface (`Surl.Api`, `Surl.Mcp` tool, control route) - any new tool or route is listed.
- For `coaps`: where the PSK lookup reads from (the live `AccountBook` per handshake?), where the judgement is published and the fault plan asked (an engine-side judge as ADR-0165 chose for TLS-SRP, or the policy), how a refusal is shaped on the wire (as an unknown identity is today, `decrypt_error`, `Cryptography/Surl.Dtls.UnitLibrary/CLAUDE.md`), and that the command-stage point gains the PSK identity as `Account`.
- `coaps` is an extended scheme (ADR-0083): judged by RFC 6347 and RFC 4279, no upstream curl measurement; the proxy and TLS-SRP servers answer upstream curl, so any byte they would send differently is measured from pinned upstream curl 8.21.0 with `Record-CurlExchange.ps1` first, or the ADR states no byte changes.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/`, with the next free number, marked "Decided by Claude under Stewart's delegation", answers the questions above for all three sources, with the reason for each in-or-out choice.
- [ ] ADR-0124 and ADR-0132 carry an "Amended by" line naming the new ADR where it changes them.
- [ ] Every wire behaviour the ADR pins cites its specification (coaps) or a measurement of pinned upstream curl 8.21.0 (proxy, TLS-SRP), or states that no byte changes.
- [ ] One feature task per library the ADR changes is filed with `task-board.ps1 new`, depending on this task, with exact `touches`, contracts before servers, host tasks (`Surl.Api`, `Surl.Mcp.UnitLibrary`, `Surl.Control.UnitLibrary`) after the composition task. Each is listed in Notes.

## Notes

## Log

- 2026-11-16: Created.
