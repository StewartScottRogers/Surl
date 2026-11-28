---
id: BL-2022
title: Prove with pinned upstream curl that a start-time seeded LDAP entry survives a reset
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2020]
touches: [Conformance/Surl.Conformance.UnitTests/StandInLdapDirectoryRecipeTests.cs]
requirement: FR-061
created: 2026-11-27
completed:
---
# BL-2022 — Prove with pinned upstream curl that a start-time seeded LDAP entry survives a reset

## Goal

A recipe test shows pinned upstream curl finding an entry seeded by `SurlServerOptions.DirectoryLdif` both before and after `SurlServer.ResetAsync`.

## Context

- ADR-0158 decision 10: the LDAP directory is the one of the three stores with an original-protocol oracle, upstream curl's `ldap://` search. Redis and DNS are extended servers and are covered by the unit tests of BL-2017 to BL-2021.
- Add to `Conformance/Surl.Conformance.UnitTests/StandInLdapDirectoryRecipeTests.cs` (BL-1169), reusing its `SearchAsync`: `PinnedUpstreamCurl.RunReferenceForProtocolAsync` on Windows (the reference curl 8.21.0 over `WinLDAP`), `RunBuildLinkedAgainstAsync` with `OpenLDAP` elsewhere (ADR-0076). Builds are pinned in `UpstreamCurlBuilds.json`; never run another curl.
- Scenario: start with `AllowAnonymous`, `ldap://127.0.0.1:0` and `DirectoryLdif` holding the base entry; add a child entry with `Directory.AddEntriesAsync`; search; `ResetAsync`; search again for the base entry and for the child. The class already expects exit 39 and the library's "No such object" text for a missing base.
- The class summary describes only runtime seeding; extend it to say what the new test shows.
- Stand-in: seeds and resets the LDAP directory through `Surl.Api` on port 0 in memory.

## Acceptance criteria

- [ ] A new `Integration` test in `StandInLdapDirectoryRecipeTests` asserts that curl exits 0 and prints the start-time entry's DN before and after the reset.
- [ ] The same test asserts that the child entry is found before the reset and that its search exits 39 after it.
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~StandInLdapDirectoryRecipeTests"` passes on Windows with the pinned build, none Inconclusive.
- [ ] The class summary says a start-time seeded entry survives a reset.
- [ ] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
