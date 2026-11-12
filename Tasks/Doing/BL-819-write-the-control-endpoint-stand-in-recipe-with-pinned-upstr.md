---
id: BL-819
title: Write the control-endpoint stand-in recipe with pinned upstream curl driving a running surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-977]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-819 — Write the control-endpoint stand-in recipe with pinned upstream curl driving a running surl

## Goal

A recipe test class shows a non-.NET test suite's whole loop with nothing but `surl` and an HTTP client: start
`surl` with `--control`, seed a mailbox through the control endpoint, read the mail over IMAP, inject a fault,
watch the exchange through the control endpoint, and reset.

## Context

- Same recipe rules as BL-817: short, commented, `[TestCategory("Integration")]` in
  `Conformance/Surl.Conformance.UnitTests`, never the Curl port, no sleeps or polling (use the control endpoint's
  bounded wait).
- `surl` is started as a process through `SurlOnLoopback.cs` (the way a shell script or a Python, Java or Node test
  suite would run it), with `smtp`, `imap`, a mailbox, `--control http://127.0.0.1:0` and `--control-token-file`.
  Pinned upstream curl 8.21.0 plays the non-.NET test suite: `--json` and `-H "Authorization: Bearer ..."` against
  BL-814's paths for seeding (`POST /v1/mail`), faults (`POST /v1/faults`), waiting (`GET /v1/events/wait`) and
  reset (`POST /v1/reset`), and `imap://` for the read. Use the paths and JSON the ADR (BL-822 decision 7) fixed,
  as BL-814 built them.
- Also show the secure defaults in the recipe: a request without the token is refused.
- Stand-in: the loopback control endpoint for non-.NET suites.

## Acceptance criteria

- [ ] `StandInControlEndpointRecipeTests` exists with tests for: seed then read over IMAP; fault then the failing
      read; wait for the IMAP exchange event; reset then the mailbox is empty; refused without the token - each
      passing on Windows with the pinned build and the built `surl`, platform-neutral.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: depends-on BL-816 replaced by BL-977 (BL-816 split into BL-1016, BL-1022, BL-972, BL-977 after timing out; Stewart asked for the split 2026-11-06).
- 2026-11-12: Backlog -> Doing.
