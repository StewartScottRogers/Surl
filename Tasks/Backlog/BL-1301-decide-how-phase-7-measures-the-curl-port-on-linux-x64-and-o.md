---
id: BL-1301
title: Decide how Phase 7 measures the Curl port on linux-x64 and osx-arm64, and file the tasks that do it
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0089-how-phase-7-measures-the-curl-port-against-surl-beside-pinned-upstream-curl.md, Documentation/Planning/Decisions/ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1301 — Decide how Phase 7 measures the Curl port on linux-x64 and osx-arm64, and file the tasks that do it

## Goal

An ADR-0089 amendment decides how a `linux-x64` and an `osx-arm64` build of the Curl port is produced, pinned in `CurlPortBuilds.json` and run beside the pinned upstream builds, and the tasks that carry it out are on the board, so Phase 7's exit criterion can be met off Windows and for the 47 `OpenLdap_` cases.

## Context

- Phase 7 is not closed (Roadmap, Milestone 7; product overview, "Built for Phase 7", "What is not measured"): `CurlPortBuilds.json` pins the port for `win-x64` only, so off Windows every `CurlPortBesideUpstreamCurlOver<Family>Tests` test is Inconclusive (ADR-0089 decision 8), and the 47 `OpenLdap_` cases in `CurlPortBesideUpstreamCurlOverLdapTests` wait for a `linux-x64` port pin, because ADR-0076's OpenLDAP upstream build is pinned for `linux-x64` only.
- What stands in the way, to weigh in the decision:
  - ADR-0089 decision 8: "Phase 7 runs on Windows ... CI does not run the port (ADR-0083 decision 2)." ADR-0083 decision 2: "CI does not run them ... no CI runner has the port's binary." Lanes run on Windows only, and no Mac is reachable from the factory's machine (`.github/workflows/ci.yml`, the comment above "Record the non-ASCII NTLM password login").
  - ADR-0083 decision 2 already allows a session to build the port from Stewart's repository (https://github.com/StewartScottRogers/Curl) at a pinned commit; "Building Stewart's own repository is not an upstream curl download and needs no approval; re-pinning is an ordinary commit". A pin is path plus SHA-256, so a binary built afresh in each CI run only matches its pin if the publish is reproducible; the decision says how the SHA-256 is fixed and checked (e.g. built once, published as an artifact or release asset and fetched by hash, as `.github/actions/upstream-curl` fetches upstream builds; or a reproducible publish checked by rebuilding).
  - Which port commit: ADR-0089 measures one commit (`b42c319ba940f66452b2b6f3e9910d46825b08fe`). Decide whether off-Windows pins use that commit, so every platform's findings compare like with like.
  - Whether a CI job that runs the port is a merge gate: Surl's correctness never depends on the port (ADR-0003 decision 6), so a red Phase 7 result must not block the dark factory's merge to `master`.
- Standing rules: upstream curl is the only oracle and the port is never evidence (ADR-0003, ADR-0089); base class library only; no new package. If the only workable route needs a new package, a download of an upstream curl build or a threshold change, stop and file that as a task assigned to Stewart instead of deciding it.
- Stand-in: not applicable; Phase 7 measures the port and changes nothing in `surl`.

## Acceptance criteria

- [ ] ADR-0089 has an amendment, marked "Decided by Claude under Stewart's delegation", that decides: who builds the `linux-x64` and `osx-arm64` port binaries and where, at which port commit, how their SHA-256 is pinned and checked, where the Phase 7 tests run with them, and that their result never gates a merge.
- [ ] The amendment states the alternatives considered and why each was not chosen, and decision 8 points to it.
- [ ] If the amendment changes ADR-0083 decision 2's "CI does not run them", ADR-0083 says so and points to the amendment.
- [ ] The follow-up tasks are filed with `task-board.ps1 new`, each sized for one run with exact `touches` and checkable criteria: at least producing and pinning the two port builds, running the Phase 7 families on each platform, recording each platform's findings in its own section of each family file (ADR-0089 decision 8), and adding any new findings to `Port-Board-Handoff.md`. Their IDs are in Notes.

## Notes

## Log

- 2026-11-16: Created.
