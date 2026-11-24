---
id: BL-1670
title: Amend ADR-0088 so a proxy on port 0 refuses its own bound end point
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Decisions/ADR-0088-http-and-https-proxy-servers.md, Application/Surl.Composition.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-24
completed:
---
# BL-1670 — Amend ADR-0088 so a proxy on port 0 refuses its own bound end point

## Goal

ADR-0088 records that a proxy or SOCKS listener on port 0 refuses its own bound end point, and the sentence saying it does not is gone.

## Context

BL-1664 made the allowed-origin rule read the end points proxy and SOCKS listeners bind (`AllowedOriginRule.WithLaterProxyEndPoints`, `Surl.Composition`'s `BoundProxyEndPoints`). Find the ADR file with `Get-ChildItem Documentation -Recurse -Filter "ADR-0088*"`; the port-0 sentence is in its implementation notes. Decided by Claude under Stewart's delegation.

## Acceptance criteria

- [ ] ADR-0088 has an amendment, marked "Decided by Claude under Stewart's delegation", stating the rule refuses a port-0 proxy's bound end point, and the old sentence is replaced.
- [ ] `Application/Surl.Composition.UnitLibrary/CLAUDE.md` mentions `BoundProxyEndPoints` in its proxy bullet.

## Notes

## Log

- 2026-11-24: Created.
