---
id: BL-156
title: Add the SSH login contract to Surl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-154]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: FR-040
created: 2026-09-29
completed: 2026-09-29
---
# BL-156 — Add the SSH login contract to Surl.Protocol.Abstractions

## Goal

`Surl.Protocol.Abstractions` holds the SSH login contract BL-154's ADR decides - a password
login over SSH's encrypted transport and a public-key login - with
`AnonymousAuthenticationPolicy` implementing it, so the SSH server (BL-162) and
`Surl.Authentication` (BL-157) can be built against it.

## Context

- Decision: BL-154's ADR, its user-authentication section (the C# types it gives).
- ADR-0032 section 6 is the pattern: every type in the shared framework, one public type per
  file, namespace `Surl.Protocol.Abstractions`; Abstractions references nothing
  (`ProtocolIsolationTests.Abstractions_ReferencesNothing`).
- The ADR shapes the contract so no existing `IAuthenticationPolicy` implementer changes:
  `Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs`,
  `Surl.Protocol.Http.UnitTests/UnitTestAuthenticationPolicy.cs`,
  `UnitTestTwoRoundAuthenticationPolicy.cs` and
  `Surl.Protocol.Mqtt.UnitTests/UnitTestRecordingAuthenticationPolicy.cs`. If implementing it
  would need one of those changed, stop: move this task to Blocked and have `task-planner`
  re-scope it, rather than widening `touches`.
- `AnonymousAuthenticationPolicy` (the shared test double, ADR-0032 section 6) implements the
  new interface the way it answers today: every login accepted unchecked
  (`PasswordLoginVerdict.AcceptedUnchecked`, ADR-0038).

## Acceptance criteria

- [x] The types BL-154's ADR gives exist in `Surl.Protocol.Abstractions.UnitLibrary` with XML
      docs, and `AnonymousAuthenticationPolicy` implements the new interface.
- [x] Tests in `Surl.Protocol.Abstractions.UnitTests` (`AuthenticationContractTests`,
      `AnonymousAuthenticationPolicyTests`) cover every new member and record.
- [x] `ProtocolIsolationTests` pass; `dotnet build -warnaserror` of the whole solution is clean
      (no other implementer broke); the fast tests are green;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Abstractions.UnitLibrary` reports 100% line
      and branch coverage and no failing member.

## Notes

- The seven types are exactly ADR-0051 section 7's, one per file. The SSH record and enum
  tests went into `AuthenticationContractTests` as this task names (the mail ones live in
  `MailAuthenticationContractTests`); a hand-written `RefusingSshPolicy` there exercises the
  interface. No existing `IAuthenticationPolicy` implementer changed. The anonymous policy
  accepts a signed key whatever its `Proof` (ADR-0051 section 6, `--allow-anonymous`).
  `Measure-CodeQuality.ps1`: 100% line, 100% branch, 0 failing members, worst CRAP 8.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ISshAuthenticationPolicy and its records are in Surl.Protocol.Abstractions; AnonymousAuthenticationPolicy implements it; gates green
