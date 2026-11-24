---
id: BL-1662
title: Prove upstream curl -b, -c and -j against surl's cookies in an Integration test
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1661, BL-1673]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: FR-015
created: 2026-11-24
completed:
---
# BL-1662 — Prove upstream curl -b, -c and -j against surl's cookies in an Integration test

## Goal

Pinned upstream curl 8.21.0, run against a live `surl` with cookies configured, writes
the expected jar with `-c`, returns the cookies with `-b`, and drops session cookies with
`-j`, in a `TestCategory=Integration` test.

## Context

FR-015's proof (root `CLAUDE.md`: a behaviour is right when a pinned upstream curl build
completes the exchange). Only builds pinned in `UpstreamCurlBuilds.json` may run.

## Acceptance criteria

- [ ] A test class in `Conformance/Surl.Conformance.UnitTests` runs pinned curl with `-c`
      and asserts the jar lines for each attribute the ADR names.
- [ ] A second run with `-b <jar>` shows the cookies on surl's event stream.
- [ ] A run with `-b <jar> -j` shows session cookies dropped and persistent ones sent.
- [ ] `dotnet test --filter "TestCategory=Integration&FullyQualifiedName~Cookie"` green
      on Windows; FR-015's status in `Requirements.md` names the test.

## Notes

## Log

- 2026-11-24: Created.
