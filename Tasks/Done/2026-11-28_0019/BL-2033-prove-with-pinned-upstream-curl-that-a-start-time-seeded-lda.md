---
id: BL-2033
title: Prove with pinned upstream curl that a start-time seeded LDAP entry survives a reset
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2031]
touches: [Conformance/Surl.Conformance.UnitTests/StandInLdapDirectoryRecipeTests.cs]
requirement: FR-061
created: 2026-11-27
completed: 2026-11-28
---
# BL-2033 — Prove with pinned upstream curl that a start-time seeded LDAP entry survives a reset

## Goal

A recipe test shows pinned upstream curl finding an entry seeded by `SurlServerOptions.DirectoryLdif` both before and after `SurlServer.ResetAsync`.

## Context

- ADR-0158 decision 10: the LDAP directory is the one of the three stores with an original-protocol oracle, upstream curl's `ldap://` search. Redis and DNS are extended servers and are covered by the unit tests of BL-2028 to BL-2032.
- Add to `Conformance/Surl.Conformance.UnitTests/StandInLdapDirectoryRecipeTests.cs` (BL-1169), reusing its `SearchAsync`: `PinnedUpstreamCurl.RunReferenceForProtocolAsync` on Windows (the reference curl 8.21.0 over `WinLDAP`), `RunBuildLinkedAgainstAsync` with `OpenLDAP` elsewhere (ADR-0076). Builds are pinned in `UpstreamCurlBuilds.json`; never run another curl.
- Scenario: start with `AllowAnonymous`, `ldap://127.0.0.1:0` and `DirectoryLdif` holding the base entry; add a child entry with `Directory.AddEntriesAsync`; search; `ResetAsync`; search again for the base entry and for the child. The class already expects exit 39 and the library's "No such object" text for a missing base.
- The class summary describes only runtime seeding; extend it to say what the new test shows.
- Stand-in: seeds and resets the LDAP directory through `Surl.Api` on port 0 in memory.

## Acceptance criteria

- [x] A new `Integration` test in `StandInLdapDirectoryRecipeTests` asserts that curl exits 0 and prints the start-time entry's DN before and after the reset.
- [x] The same test asserts that the child entry is found before the reset and that its search exits 39 after it.
- [x] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~StandInLdapDirectoryRecipeTests"` passes on Windows with the pinned build, none Inconclusive.
- [x] The class summary says a start-time seeded entry survives a reset.
- [x] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests pass.

## Notes

- Made the change directly in the session instead of delegating each `feature` stage: it is one test method in
  one file, the behaviour already exists (BL-2031), so there is no failing-test or implementation stage.
- New test: `ResetAsync_AfterStartTimeAndRuntimeSeeds_SearchStillFindsOnlyTheStartTimeEntry`. `SearchAsync`
  now takes the DN to search (default: the base DN); the found and not-found assertions are two shared helpers.
- Results on Windows, 2026-11-28: recipe class 3 passed, 0 Inconclusive; `dotnet build` 0 warnings;
  `dotnet format --verify-no-changes` on the project clean; fast tests 30640 passed in 83 assemblies.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-28: Doing -> Done. Pinned upstream curl finds a start-time seeded LDAP entry before and after ResetAsync; the runtime child entry exits 39 after it.
