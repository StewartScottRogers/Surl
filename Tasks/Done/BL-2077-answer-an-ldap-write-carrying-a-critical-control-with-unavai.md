---
id: BL-2077
title: Answer an LDAP write carrying a critical control with unavailableCriticalExtension
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2077 — Answer an LDAP write carrying a critical control with unavailableCriticalExtension

## Goal

An add, delete, modify or modify DN request with a control marked critical is answered `unavailableCriticalExtension` (12), `critical control not supported`, in that operation's response.

## Context

- Found while working BL-2060 (2026-11-28), by reading the code; no test or run has shown it.
- ADR-0072 decision 3, row "A control marked critical": `unavailableCriticalExtension` (12) "for the operation (no control is supported)". RFC 4511 section 4.1.11.
- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapSession.cs`: `ResponseTagOf` returns `null` for the writes, which decode as `LdapUnrecognizedOperation`, so `AnswerAsync` skips the critical-control check and the write refusal (`unwillingToPerform`) answers.
- Abandon and unbind keep ignoring the control.

## Acceptance criteria

- [x] A test in `LdapProtocolServerOperationTests` sends each of add, delete, modify and modify DN with a critical control and reads `unavailableCriticalExtension` `critical control not supported` in the matching response.
- [x] The existing test of abandon and unbind with a critical control still passes unchanged.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- `LdapSession.ResponseTagOf` now gives the four writes their response tags, so the existing
  critical-control check answers them. The new test
  `ServeAsync_CriticalControlOnAWrite_IsUnavailableCriticalExtension` (4 rows) failed with
  `unwillingToPerform` before the change and passes after it.
- No new ADR: the code now does what ADR-0072 decision 3 already says. No upstream curl
  measurement: no curl case sends a write or a control marked critical on one, and the bytes are
  RFC 4511's.
- As for search and compare, the critical-control refusal comes before the fault plan is asked, so
  a fault rule on that write is not applied to it.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. An LDAP add, delete, modify or modify DN with a critical control is answered unavailableCriticalExtension in its own response
