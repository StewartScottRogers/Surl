---
id: BL-1682
title: Record the Phase 7 findings as filed on the port's board and accept entry 28's two cases
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Documentation/Conformance/CurlPort, Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-11-24
completed:
---
# BL-1682 — Record the Phase 7 findings as filed on the port's board and accept entry 28's two cases

## Goal

Surl's Phase 7 documents say every finding in `Port-Board-Handoff.md` is filed on the Curl
port's board, with each entry's port task ID, and no longer say filing is still Stewart's.

## Context

BL-1658 closed 2026-11-24: the port's session `curl-50` ran the port's `/task-plan` on
`Documentation/Conformance/CurlPort/Port-Board-Handoff.md`. Its answer:

- Entries 1-23: already filed 2026-11-16 as port BL-5279..BL-5297 (all Done except BL-5292
  and BL-5293, Deferred). Entries 7, 20, 21 and 22 were covered then by port BL-4899 and
  BL-4965..BL-4968, BL-5043, BL-4941, and BL-4903/BL-4956.
- Entries 24-29: filed as port BL-5905..BL-5910 (port branch `work/dark-factory`, commit
  `a6ddaf797f`): 24 → BL-5905 (FTP `-P` slow, test 251), 25 → BL-5906 (HTTP/2 RST_STREAM after
  Digest 401), 26 → BL-5907 (OpenLDAP SASL bind, depends on port BL-5853), 27 → BL-5908
  (OpenLDAP StartTLS), 28 → BL-5909, 29 → BL-5910 (exit 25, chunked PUT to HTTP/1.0, cases
  1072/1073).
- Entry 28: decided on the port's side as a known difference. The port keeps downloading over
  `blowfish-cbc`/`cast128-cbc` and does not copy macOS libssh2's crash (exit 139); port BL-5909
  records the ADR. Surl may list those two cases as accepted differences. This is the port's
  choice about its own behaviour, not evidence about Surl (ADR-0003): Surl's verdict on those
  cases still comes from pinned upstream curl.

The roadmap's Milestone 7 status still ends "Still Stewart's: filing the 31 findings on the
port's board".

## Acceptance criteria

- [ ] `Port-Board-Handoff.md` names the port task ID beside every one of its 29 entries.
- [ ] The Phase 7 findings file holding entry 28 lists its two cases as an accepted difference,
      citing port BL-5909, and says upstream curl's verdict is unchanged.
- [ ] `Roadmap.md` no longer says filing the findings is still Stewart's.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-25: Renumbered from BL-1668, which a lane used for a different task at the same time.
