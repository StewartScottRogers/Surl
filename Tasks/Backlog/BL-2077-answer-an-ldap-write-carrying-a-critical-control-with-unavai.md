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
completed:
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

- [ ] A test in `LdapProtocolServerOperationTests` sends each of add, delete, modify and modify DN with a critical control and reads `unavailableCriticalExtension` `critical control not supported` in the matching response.
- [ ] The existing test of abandon and unbind with a critical control still passes unchanged.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-11-28: Created.
