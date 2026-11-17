---
id: BL-1315
title: Pin the libtest programs by SHA-256 as kind libtest and verify them in CI (ADR-0174)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1310, BL-1314]
touches: [UpstreamCurlBuilds.json, .github/actions/upstream-curl, Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1315 — Pin the libtest programs by SHA-256 as kind libtest and verify them in CI (ADR-0174)

## Goal

UpstreamCurlBuilds.json pins each libtest program by SHA-256 as kind libtest, and CI verifies them.

## Context

ADR-0174 decision 2 and 4; follows the Debug pin of BL-1310 and the .github/actions/upstream-curl debug input.

## Acceptance criteria

- [x] UpstreamCurlBuilds.json has a linux-x64 entry kind libtest, role supplementary, with a programs map of name to SHA-256.
- [x] The upstream-curl action fails when any program hash differs from the pin, and a test pins the schema.

## Notes

- 2026-11-16: The libtest programs are not built yet (BL-1314 builds them in Build-DebugUpstreamCurl.ps1), so there are no real SHA-256 values to pin. BL-1314 added to depends-on; pin and CI verification wait for it.

- 2026-11-16: Built the Debug script (Docker, curl hash matched its pin, so reproducible). curl 8.21.0 links all libtests into ONE binary, `libtests`, so the programs map has that one entry (040083EC...). Added Conformance projects to touches (no other Doing task touches them): `UpstreamCurlBuildPins` needed `Libtest` kind, optional sha256 and a `Programs` map, else the pin file would not parse. Action step "Verify the libtest programs" hashes each program under the pin directory. The runner path is ADR-0174 decision 3, a later task.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Backlog. Needs BL-1314 first: the libtest programs are not built, so there are no hashes to pin
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. libtest programs pinned by SHA-256 as kind libtest; parser, action check and tests added
