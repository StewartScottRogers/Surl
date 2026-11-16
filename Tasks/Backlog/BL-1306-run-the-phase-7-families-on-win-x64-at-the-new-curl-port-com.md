---
id: BL-1306
title: Run the Phase 7 families on win-x64 at the new Curl port commit and update their findings
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1305]
touches: [Documentation/Conformance/CurlPort, Conformance/Surl.Conformance.UnitTests]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1306 — Run the Phase 7 families on win-x64 at the new Curl port commit and update their findings

## Goal

Every Windows Phase 7 family is re-run at the new port commit and each findings file names that commit with its findings brought up to date.

## Context

ADR-0089 decisions 5 and 10 and Amendment 8: a stale finding fails its test, so update each findings file in the same change. Findings files: `Documentation/Conformance/CurlPort/*.md`.

## Acceptance criteria

- [ ] Every `CurlPortBesideUpstreamCurlOver<Family>Tests` test passes on Windows against the new pin.
- [ ] Each findings file names the new commit and SHA-256, and lists which earlier findings the new commit fixed or added.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-16: Created.
