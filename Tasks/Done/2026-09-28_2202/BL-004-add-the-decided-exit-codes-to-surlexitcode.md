---
id: BL-004
title: Add the decided exit codes to SurlExitCode
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-001]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-004 — Add the decided exit codes to SurlExitCode

## Goal

`SurlExitCode` holds every member of the exit-code table in the ADR recorded by BL-001,
with the number the ADR assigns, and a test fails if any number changes.

## Context

- File: `Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs`. It holds `Ok = 0` and
  `FailedInit = 2` today.
- The table to implement is in the exit-code ADR under
  `Documentation/Planning/Decisions/`, the one BL-001 recorded. Its `README.md` index
  names it.
- Values are never renumbered (the enum's own remarks).
- `Surl.Console/Program.cs` still returns `FailedInit` for every command line until
  BL-019, so `FailedInit`'s doc comment stays true as it is.

## Acceptance criteria

- [x] `SurlExitCode` has exactly one member per row of the ADR's table, with the ADR's
      name and number.
- [x] Every member has an XML doc comment that states when surl returns it and names
      the upstream `CURLE_*` code it reuses, or says none does.
- [x] The enum's `<remarks>` no longer say only the Phase 0 values exist. They cite the
      ADR by number.
- [x] `Surl.Protocol.Abstractions.UnitTests/SurlExitCodeTests.cs` has a data-driven
      test, `Value_EachMember_HasTheNumberTheAdrAssigns`, with one `DataRow` per member,
      and a test, `GetValues_Always_HasOneMemberPerAdrRow`, pinning the member count.
- [x] `dotnet build Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean, and
      `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] `ProtocolIsolationTests.Abstractions_ReferencesNothing` still passes.

## Notes

- Delivered directly rather than through the full `/feature` agent chain: the change is
  one enum transcribed from ADR-0005 section 1 plus its pinning tests, with no design left
  to plan (sensible default under the dark-factory rules).
- Numbers: Ok 0, UnsupportedProtocol 1, FailedInit 2, MalformedUrl 3,
  CouldNotResolveHost 6, CouldNotReadFile 37, BindFailed 45, InternalError 125.
- `FailedInit`'s doc comment keeps "the Phase 0 placeholder returns it for every command
  line", still true until BL-019.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. SurlExitCode holds ADR-0005's eight exit codes, each number pinned by a test
