---
id: BL-1899
title: Say in the coap help text that a removed last account does not open reads
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1860]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1899 — Say in the coap help text that a removed last account does not open reads

## Goal

The `coap` `--aihelp` paragraph says a read stays refused after the last account `coap` has had is removed.

## Context

- ADR-0132 amendment 5, decisions 1 to 3.
- `AiHelpProse`'s `coap` paragraph beginning "`coap` is NoSec" says "A read is served unless an account makes the
  server private". BL-1860 names only the auth, http, RTSP and WebSocket paragraphs. Found by BL-1862.

## Acceptance criteria

- [x] The paragraph says, or points to the auth topic for, the rule that once `coap` has had an account a read needs
      a login until a reset or restore returns a state in which it had none.
- [x] `ManualTextTests` and the `AiHelpTextTests` pass.

## Notes

- 2026-11-27: Rewrote the `coap` paragraph in `AiHelpProse` to state the rule itself and still point to the auth
  topic. Checked against `CoapAccessRules` and `ExtendedProtocolServers.CoapAccessRulesFor`: reads also stay refused
  while `--user-ldap` serves `coap`, and `--allow-anonymous` serves every read. The old wording could be read as
  `--allow-anonymous` refusing reads.
- The paragraph says "over `coap`" for the read and write rules, because an authenticated `coaps` peer reads and
  writes without `--allow-anonymous`.
- `ManualText` has no sentence about CoAP, so it did not change.
- Verification: `dotnet build` clean; `Surl.Cli.UnitTests` 1992 passed; `dotnet format --verify-no-changes` clean on
  `Surl.Cli.UnitLibrary`. In the full fast run one test outside this task failed
  (`SocketIoFaultTests.Connect_UnderADelayRule_IsServedAfterTheHold`, timeout) and its project passed 291 of 291 when
  rerun. Filed BL-1905.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The coap --aihelp paragraph says reads stay refused after the last account coap has had is removed, until a reset or restore returns a state with none
