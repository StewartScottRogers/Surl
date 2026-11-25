---
id: BL-1629
title: Bring the Roadmap, Product-Overview, Requirements FR-057 and CurlPort README in line with Phase 7 on linux-x64 and osx-arm64
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1624, BL-1625, BL-1626, BL-1627, BL-1628]
touches: [Documentation/Planning/Roadmap.md, Documentation/Product/Product-Overview.md, Documentation/Product/Requirements.md, Documentation/Conformance/CurlPort/README.md]
requirement: FR-057
created: 2026-11-24
completed: 2026-11-24
---
# BL-1629 — Bring the Roadmap, Product-Overview, Requirements FR-057 and CurlPort README in line with Phase 7 on linux-x64 and osx-arm64

## Goal

The four documents that summarise Phase 7 state what is true after BL-1624 to BL-1628: the port commit measured, the platforms measured and their results, what ADR-0089's new amendment (BL-1627) records as not measurable per platform, and whether Phase 7 is closed.

## Context

Each document is stale in a known way (checked 2026-11-24):
- `Documentation/Planning/Roadmap.md`, "Milestone 7 — Phase 7": Status names port commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (replaced by `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, ADR-0089 Amendment 8), says `CurlPortBuilds.json` pins the port for `win-x64` alone (it also pins linux-x64 and osx-arm64), says the 47 `OpenLdap_` cases wait for a linux-x64 pin "(no task filed)" (they ran in run 44044472038), and counts 23 findings (the handoff has 24 win-x64 entries plus BL-1628's). Its Exit criteria say "not met for other platforms".
- `Documentation/Product/Product-Overview.md`, "Built for Phase 7: the Curl port measured beside pinned upstream curl": "Measured" names `b42c319…` and `curl.exe` SHA-256 `F28D1417…`, "on `win-x64` only"; "Handoff" says 23 findings; "What is not measured" says every platform but win-x64 is Inconclusive and the `OpenLdap_` cases wait for a linux-x64 pin.
- `Documentation/Product/Requirements.md`, row FR-057: "Windows only until a non-Windows port pin exists", port commit `b42c319…`, "18 findings", "not yet measured: every platform but `win-x64` … the 47 `OpenLdap_` cases … 15 of the extended clients, whose Integration tasks have not run".
- `Documentation/Conformance/CurlPort/README.md`, "What was measured": "Platform: Windows (`win-x64`) only", the osx-arm64 run "compared nothing", "`CurlPortBuilds.json` pins the port for `win-x64` alone", the OpenLDAP build "stay Inconclusive until a `linux-x64` port pin exists"; the Results table and "Not counted above" cover win-x64 only.

Take numbers and findings from the family files and `Port-Board-Handoff.md` as BL-1624 to BL-1628 left them, and the closing condition and not-measurable list from BL-1627's ADR-0089 amendment. Phase 7 is "closed" on a platform only if that amendment's condition holds; if it does not, say which part is open and which task holds it. Filing the findings on the port's board is Stewart's and stays listed as his, not as a task. The Curl port is never an oracle (ADR-0003). Docs only; no code.

## Acceptance criteria

- [x] `Roadmap.md` "Milestone 7 — Phase 7" Status and Exit criteria name port commit `74f3f45c…`, state per platform (win-x64, linux-x64, osx-arm64) whether the BL-1627 closing condition holds, name the run each platform was measured in, and no longer mention `b42c319…` except as the commit it replaced, nor "no task filed".
- [x] `Product-Overview.md` "Built for Phase 7" Measured / Result / Handoff / "What is not measured" state the same commit, the platforms and their counts, the handoff's total entry count, and the not-measurable groups with BL-1627's reasons.
- [x] `Requirements.md` FR-057 states the same commit, platforms, finding count and not-measured list, and no longer says "Windows only until a non-Windows port pin exists".
- [x] `CurlPort/README.md` "What was measured" lists the three platforms with their port SHA-256 pins from `CurlPortBuilds.json` and their upstream builds from `UpstreamCurlBuilds.json`, the Results section has the linux-x64 and osx-arm64 counts per family (a table per platform or a column per platform), and "The handoff" states the handoff's total.
- [x] A search of the four files for `b42c319`, "win-x64 alone", "Windows only" and "compared nothing" finds each only where the text says it is history.

## Notes

Amendment 9's closing condition 4 (a literal "not measurable on <platform>: <reason>" line in each family file) holds in no family file, so the four documents say Phase 7 is open on linux-x64 and osx-arm64 in that one part, and name BL-1648, filed for it. Per-platform counts in CurlPort/README.md are the run's raw counts before triage, as the family files give them. The 31 findings = 24 win-x64 + 7 from BL-1626/BL-1625, matching the handoff.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Four documents state port commit 74f3f45, three platforms, 31 findings, Amendment 9 not-measurable groups; condition 4 open under BL-1648
