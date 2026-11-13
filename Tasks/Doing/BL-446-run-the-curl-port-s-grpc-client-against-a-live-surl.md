---
id: BL-446
title: Run the Curl port's grpc client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1093, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Grpc.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-446 — Run the Curl port's grpc client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl grpc://` and `surl grpcs://`. They make
a unary call described by reflection, a call with a `.proto` file, a call to an unknown method, and an
unauthenticated call. Each assertion comes from PROTOCOL-HTTP2.md and statuscodes.md, and the matrix is fully
covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help grpc` and its cases in `Curl.Conformance.Extended.UnitTests/Grpc/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [ ] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [ ] `Documentation/Conformance/Extended/Grpc.md`: every row is covered by a named test or marked `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-12: depends-on BL-445 repointed to BL-1093 (BL-445 split into BL-1094, BL-1095, BL-1093).
- 2026-11-12: Backlog -> Doing.
