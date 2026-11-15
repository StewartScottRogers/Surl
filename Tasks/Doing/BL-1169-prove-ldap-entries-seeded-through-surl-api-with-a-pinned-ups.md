---
id: BL-1169
title: Prove LDAP entries seeded through Surl.Api with a pinned upstream curl ldap search
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1166]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: FR-061
created: 2026-11-14
completed:
---
# BL-1169 — Prove LDAP entries seeded through Surl.Api with a pinned upstream curl ldap search

## Goal

A stand-in recipe test shows pinned upstream curl 8.21.0 finding, over `ldap://`, an entry a test seeded through `Surl.Api` into an in-memory server, and not finding it after `ResetAsync`.

## Context

- Recipes live in `Conformance/Surl.Conformance.UnitTests` as `StandIn*RecipeTests.cs` (for example `StandInMailRoundTripRecipeTests.cs`), tagged `TestCategory=Integration`, and run a pinned build located through `UpstreamCurlLocator` (`UpstreamCurlBuilds.json`). Upstream curl is the only oracle (ADR-0003); never the Curl port.
- LDAP needs a curl build with LDAP: copy how `UpstreamCurlSearchesSurlOverLdapTests.cs` and `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.cs` pick their build and skip where it is missing (the OpenLDAP build of ADR-0076/ADR-0104 in CI; the Windows reference build uses WinLDAP).
- Expected output is what pinned curl prints for the search (LDIF-like `DN:` lines, upstream's `ldap.c` format), measured on the run, and its exit code 0; after reset, the search returns no entry and exit code 0 (or what upstream returns for `noSuchObject`, measured - curl 8.21.0 maps an LDAP error to exit 39, `CURLE_LDAP_SEARCH_FAILED`, per https://curl.se/libcurl/c/libcurl-errors.html).

## Acceptance criteria

- [ ] A new class `StandInLdapDirectoryRecipeTests` in `Conformance/Surl.Conformance.UnitTests` starts `SurlServer` on `ldap://127.0.0.1:0` in memory, seeds one LDIF entry, and asserts pinned upstream curl 8.21.0's stdout holds its DN and attribute and its exit code is 0.
- [ ] The same class shows that after `ResetAsync` the same curl command no longer prints the entry, pinning the exit code measured from the pinned build.
- [ ] The tests are `[TestCategory("Integration")]`, skip with a stated reason where no LDAP-capable pinned build exists, and pass on Windows and the CI LDAP leg.
- [ ] `Documentation/Wiki/Surl-As-A-Stand-In.md` is not edited here (BL-1172 documents it).

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
