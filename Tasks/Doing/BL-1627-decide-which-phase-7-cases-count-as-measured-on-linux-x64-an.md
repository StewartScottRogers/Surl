---
id: BL-1627
title: Decide which Phase 7 cases count as measured on linux-x64 and osx-arm64 and which are recorded as not measurable there
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md]
requirement: FR-057
created: 2026-11-24
completed:
---
# BL-1627 — Decide which Phase 7 cases count as measured on linux-x64 and osx-arm64 and which are recorded as not measurable there

## Goal

ADR-0089 gains an amendment, decided by Claude under Stewart's delegation, that says what "Phase 7 is closed on a platform" means for linux-x64 and osx-arm64: which cases must be compared there, and which are recorded as not measurable on that platform and why.

## Context

Roadmap "Milestone 7 — Phase 7" says the exit criterion is "met for `win-x64` … not met for other platforms", without saying what met means off Windows. Some cases cannot be compared off Windows by construction, whatever the workflow does:
- **LDAP, LDAPS and RTSP on osx-arm64.** The osx-arm64 reference build in `UpstreamCurlBuilds.json` (`04E0E69B…EEBF`, curl 8.21.0) lists no `ldap` and no `rtsp` protocol, and the OpenLDAP supplementary build is built for Linux only (`.github/actions/upstream-curl`, `openldap` input), so the 47 `OpenLdap_` cases and the RTSP family have no oracle on osx-arm64.
- **`WinLdap_` cases** need upstream curl linked against WinLDAP; BL-1343 made them Inconclusive off Windows.
- **The libcurl rows** (`CurlPortLibcurlBesideUpstreamLibcurlOver*Tests`, `Libcurl.md`): upstream's static Linux and macOS builds carry no shared libcurl (`UpstreamCurlLocation` "no pinned libcurl"), and `CurlPortBuilds.json` pins the port's `Libcurl.dll` for win-x64 only.
- **Cases under `[OSCondition(OperatingSystems.Windows)]`** in the family classes (for example HTTP's Negotiate and Windows-build cases).
- **The extended clients** (`Extended.md`): judged by their specifications, not beside upstream curl (ADR-0089 decision 9); their `CurlPortClient*Tests` and `PinnedCurlPortClientTalksToSurlOver*Tests` are not in the workflow's `FullyQualifiedName~CurlPortBesideUpstreamCurl` filter, so they have never run off Windows.

Decide, by the standing rules (upstream curl is the only oracle, ADR-0003; the port is never one; the simplest faithful answer), for each group: is it out of scope on that platform (recorded as not measurable, with the reason), or must it be measured before Phase 7 closes there — and if the latter, what work does it need. Pinning another upstream curl build (for example an OpenLDAP or rtsp-capable macOS build) is a download that needs Stewart's approval: if the decision wants one, file it as a `Stewart` task rather than assuming it. Widening the workflow's filter to the extended clients is Claude's to decide; if decided, file it as a `direct` task on `.github/workflows/curl-port-phase-7.yml`.

The single-commit rule needs no change: ADR-0089 Amendment 8 replaced `b42c319` with `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, win-x64 was re-measured at it on 2026-11-16 (`Port-Board-Handoff.md`, "What the new commit fixed"), and `CurlPortBuilds.json` pins linux-x64 and osx-arm64 at the same commit. Say so in the amendment so no reader re-measures win-x64.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md` has a new numbered amendment, marked "Decided by Claude under Stewart's delegation" and naming BL-1627, that lists each group above with its verdict per platform (must be measured / not measurable, with the reason) and the alternatives considered.
- [ ] The amendment states the closing condition for a platform as something checkable from the repository (for example: every family file's platform section names a completed run, and every failure of that run is listed in a platform findings table).
- [ ] The amendment states that port commit `74f3f45c…` is the one commit for all three platforms and win-x64 needs no re-measurement.
- [ ] Any follow-up the decision calls for is filed as a task (a `Stewart` task for any upstream build download) and listed in this task's Notes; if none, the Notes say so.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
