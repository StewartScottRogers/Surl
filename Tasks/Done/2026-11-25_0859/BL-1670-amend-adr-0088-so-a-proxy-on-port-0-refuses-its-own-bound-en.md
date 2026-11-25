---
id: BL-1670
title: Amend ADR-0088 so a proxy on port 0 refuses its own bound end point
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md, Application/Surl.Composition.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1670 — Amend ADR-0088 so a proxy on port 0 refuses its own bound end point

## Goal

ADR-0088 records that a proxy or SOCKS listener on port 0 refuses its own bound end point, and the sentence saying it does not is gone.

## Context

BL-1664 made the allowed-origin rule read the end points proxy and SOCKS listeners bind (`AllowedOriginRule.WithLaterProxyEndPoints`, `Surl.Composition`'s `BoundProxyEndPoints`). Find the ADR file with `Get-ChildItem Documentation -Recurse -Filter "ADR-0088*"`; the port-0 sentence is in its implementation notes. Decided by Claude under Stewart's delegation.

## Acceptance criteria

- [x] ADR-0088 has an amendment, marked "Decided by Claude under Stewart's delegation", stating the rule refuses a port-0 proxy's bound end point, and the old sentence is replaced.
- [x] `Application/Surl.Composition.UnitLibrary/CLAUDE.md` mentions `BoundProxyEndPoints` in its proxy bullet.

## Notes

- Added ADR-0088 Amendment 11 and replaced Amendment 6's port-0 sentence; added `BoundProxyEndPoints` to the proxy bullet. Decided by Claude under Stewart's delegation.
- `touches` named `Documentation/Decisions/ADR-0088-http-and-https-proxy-servers.md`, which does not exist; corrected to the real file under `Documentation/Planning/Decisions`. No task in Doing names it.
- Made the two edits in the session instead of delegating to `align-and-document`: both were already read and small, so a second agent would only have spent tokens. No `.cs` or project file changed, so the `verify` skill was not needed; `dotnet build` (0 warnings) and the fast tests (all passed) were run.
- The ADR index row in `Documentation/Planning/Decisions/README.md` says nothing about port 0, so it is unchanged.
- Follow-up filed: BL-1681 (`BoundProxyEndPoints.Record`'s doc comment says "on a port the system chose"; the code records fixed ports too).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. ADR-0088 Amendment 11 records that a proxy or SOCKS listener on port 0 refuses its own bound end point
