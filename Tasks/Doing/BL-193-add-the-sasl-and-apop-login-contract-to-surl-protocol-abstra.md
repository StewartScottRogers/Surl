---
id: BL-193
title: Add the SASL and APOP login contract to Surl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-185]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: FR-046
created: 2026-09-29
completed:
---
# BL-193 — Add the SASL and APOP login contract to Surl.Protocol.Abstractions

## Goal

`Surl.Protocol.Abstractions` holds the SASL and `APOP` login contract BL-185's ADR decides, with
`AnonymousAuthenticationPolicy` implementing it, so the mail servers (BL-200, BL-204, BL-206)
and `Surl.Authentication` (BL-194 to BL-196) can be built against it.

## Context

- Decision: BL-185's ADR, its contract section (the C# it gives).
- ADR-0032 section 6 is the pattern: shared-framework types only, one public type per file,
  namespace `Surl.Protocol.Abstractions`; Abstractions references nothing.
- The ADR shapes the contract as a new interface so no existing `IAuthenticationPolicy`
  implementer changes (`Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs`, the test
  doubles in `Surl.Protocol.Http.UnitTests` and `Surl.Protocol.Mqtt.UnitTests`). If it would need
  one changed, stop, move this task to Blocked and have `task-planner` re-scope it.
- `AnonymousAuthenticationPolicy` implements it as it answers today: every exchange accepted
  unchecked (`AcceptedUnchecked`, ADR-0038), in one step.

## Acceptance criteria

- [ ] The types BL-185's ADR gives exist with XML docs, and `AnonymousAuthenticationPolicy`
      implements the new interface.
- [ ] Tests in `Surl.Protocol.Abstractions.UnitTests` cover every new member and record.
- [ ] `ProtocolIsolationTests` pass; `dotnet build -warnaserror` of the whole solution is clean;
      the fast tests are green; `Measure-CodeQuality.ps1 -Library
      Surl.Protocol.Abstractions.UnitLibrary` reports 100% line and branch coverage and no
      failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
