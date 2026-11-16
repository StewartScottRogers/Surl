---
id: BL-1288
title: Correct the Roadmap's and product overview's Phase 7 and extended-track statements that BL-1124 made stale
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Roadmap.md, Documentation/Product/Product-Overview.md]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1288 — Correct the Roadmap's and product overview's Phase 7 and extended-track statements that BL-1124 made stale

## Goal

The Roadmap's Milestone 7 and "Track — Extended servers" and the product overview's "Built for Phase 7" section state what `Documentation/Conformance/CurlPort/` records now: 23 findings in the handoff, and every extended client's `Integration` result recorded in `Extended.md`.

## Context

- BL-1124 (Done 2026-11-14) refreshed `Documentation/Conformance/CurlPort/Extended.md` to all 19 extended clients, added findings EXT-2 to EXT-6 and handoff entries 19 to 23. `Documentation/Conformance/CurlPort/README.md` now totals 1,518 cases, 1,420 agreements, 96 port defects, **23** findings, 2 not comparable, and `Port-Board-Handoff.md` holds 23 entries.
- Still stale, found by `/task-plan` on 2026-11-16:
  - `Documentation/Planning/Roadmap.md`, Milestone 7: "96 port defects behind 18 findings"; "`Extended.md` still records 4 of the 19 extended clients ... (BL-1124 refreshes it)"; "Stewart has not yet filed the 18 findings"; and the exit criteria's "the remaining extended clients".
  - `Roadmap.md`, "Track — Extended servers": "the Curl port's client has run against 4 of them" and "`Extended.md` has not been refreshed from them (BL-1124)".
  - `Documentation/Product/Product-Overview.md`, "Built for Phase 7": "behind 18 findings", "holds the 18 findings", and the "What is not measured" bullet "The extended clients beyond `socketio`, `webtransport`, `coap` and `xmpp`: the other 15 `Integration` tasks have not run".
- Before writing, re-read `Extended.md` and the CurlPort `README.md`: if their numbers differ from the ones above, the files win. Whether the 96 port-defect total includes the EXT findings is whatever README's "All" row says.
- Stand-in: not applicable; this is a statement of what was measured, with no store, event or fault involved.

## Acceptance criteria

- [ ] `Roadmap.md` Milestone 7 and the product overview's "Built for Phase 7" give the same case, agreement, port-defect, finding and not-comparable totals as the "All" row of `Documentation/Conformance/CurlPort/README.md`.
- [ ] Neither file says any extended client's `Integration` run is missing from `Extended.md`, and neither names BL-1124 as work to come.
- [ ] `Roadmap.md`'s "Track — Extended servers" Status says `Extended.md` records all 19 extended clients (or the number `Extended.md` actually records).
- [ ] What stays open stays stated: the port pinned for `win-x64` only, the 47 `OpenLdap_` cases, and Stewart filing the handoff on the port's board.
- [ ] `git grep -n "18 findings" -- Documentation/Planning/Roadmap.md Documentation/Product/Product-Overview.md` prints nothing.

## Notes

## Log

- 2026-11-16: Created.
