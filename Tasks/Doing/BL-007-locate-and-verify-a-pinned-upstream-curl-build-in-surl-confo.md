---
id: BL-007
title: Locate and verify a pinned upstream curl build in Surl.Conformance
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-007 — Locate and verify a pinned upstream curl build in Surl.Conformance

## Goal

`Surl.Conformance.UnitLibrary` finds the pinned upstream curl build for the running
platform and refuses any curl whose SHA-256 is not pinned. This is the C# counterpart of
`Assert-PinnedUpstreamCurl` in `Record-CurlExchange.ps1`, so every conformance test runs
only upstream curl.

## Context

- ADR-0003, decision 3: `Record-CurlExchange.ps1` refuses any curl whose SHA-256 is not
  pinned, "and every later tool that runs curl for Surl (the conformance tests first)
  does the same". The Curl port reports upstream's version number, so only the file's
  hash identifies upstream curl.
- `UpstreamCurlBuilds.json` at the repository root. Each entry of `builds` has
  `platform` (today only `win-x64`), `defaultPath`, `sha256` (upper-case hex), `version`,
  `protocols`, `features`, `pinned`.
- `Record-CurlExchange.ps1`, function `Assert-PinnedUpstreamCurl`: hashes the file and
  refuses it with the message "`<path>` (SHA-256 `<hash>`) is not a pinned upstream curl
  build. Surl is measured only against the builds in UpstreamCurlBuilds.json (ADR-0003);
  pinning another is a decision, and the Curl port is never one."
- `Surl.Conformance.UnitLibrary/CLAUDE.md`: parsing and matching are unit tested with no
  process and no network. It is a production project, so it is AOT-compatible: parse the
  JSON with `System.Text.Json.JsonDocument` (or a source-generated context), never the
  reflection-based serializer.
- Tests that touch the file system are `[TestCategory("Integration")]`
  (`.claude/rules/testing.md`). Give the locator its JSON as text and its file access
  through an injected seam (does the file exist, open it for reading), so the fast tests
  need no disk.
- Linux and macOS have no pinned build yet (product overview, open question 2). There,
  the locator reports that no pinned build exists for the platform. Later integration
  tests turn that into `Assert.Inconclusive`, not a failure, so CI stays green.

## Acceptance criteria

- [ ] A type in `Surl.Conformance.UnitLibrary` parses the text of
      `UpstreamCurlBuilds.json` into pinned-build records carrying platform, default
      path, SHA-256, version and protocols. Malformed JSON, a missing `builds` array or
      an entry missing `sha256` or `defaultPath` produce a failure naming the problem.
- [ ] Each pinned-build record also carries its `role`, `reference` or
      `supplementary`, and a missing `role` reads as `reference`. BL-026 adds the field
      and a supplementary 8.22.0 build used only for SMB, HTTP/2 and HTTP/3.
- [ ] A locator, given the parsed pins, a platform name such as `win-x64`, a role
      (default `reference`) and the file-access seam, returns the first build of that
      platform and role whose `defaultPath` exists and whose SHA-256 (hex, compared
      case-insensitively) matches its pin. A fast test proves that the default never
      returns a supplementary build.
- [ ] Given a file whose hash matches no pin, the locator refuses it with a message
      that contains the path, the hash and the phrase "is not a pinned upstream curl
      build", the same wording as `Assert-PinnedUpstreamCurl`.
- [ ] Given a platform with no pinned build, or a pinned build whose file is absent, the
      locator returns a "not available" result that says which, and throws nothing.
- [ ] Fast tests in `Surl.Conformance.UnitTests` cover every path above, with the JSON
      and file bytes supplied in memory. Test names follow
      `MethodName_Condition_ExpectedResult`.
- [ ] One `[TestCategory("Integration")]` test reads the real `UpstreamCurlBuilds.json`
      from the repository root and parses it without failure.
- [ ] `dotnet build Surl.Conformance.UnitLibrary -warnaserror` is clean, and
      `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Conformance.UnitLibrary`. The production seam's real file-access class is
      either covered or carries `[ExcludeFromCodeCoverage]` with a justifying comment
      above it.

## Notes

This task runs no curl process. BL-020 runs the build this locator finds.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
