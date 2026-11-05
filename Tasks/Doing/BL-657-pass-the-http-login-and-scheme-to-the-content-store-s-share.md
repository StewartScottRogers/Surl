---
id: BL-657
title: Pass the HTTP login and scheme to the content store's share access check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-656]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-657 — Pass the HTTP login and scheme to the content store's share access check

## Goal

The HTTP server (HTTP/1.x and every HTTP/2 or HTTP/3 stream it answers through the same code) hands the content
store the request's checked login and scheme, and answers a share-access refusal with the status BL-642's ADR
measured against upstream curl.

## Context

- Decision: BL-642's ADR decision 2 (the HTTP status for a denied read and a denied write, and what body; measured
  against pinned upstream curl 8.21.0 with curl's exit code). ADR-0008 (how the HTTP server answers), ADR-0032
  section 4 (HTTP policy: the checked account name is on the verdict), BL-656's caller API.
- Where: `Protocols/Http/Surl.Protocol.Http.UnitLibrary` (`HttpRequestAnswerer` and its content-store calls). If
  part of the call path lives in `Protocols/Http/Surl.HttpServing.UnitLibrary` (ADR-0084), and changing it is
  needed, stop and record in Notes: that library is outside this task's touches.
- Oracle: pinned upstream curl only. `UpstreamCurlFetchesFromSurlOverHttp11Tests`, `…OverHttpsTests` and
  `UpstreamCurlLogsInToSurlOverHttpTests` must pass unchanged.

## Acceptance criteria

- [ ] Named tests replay requests where: an account with `r` access GETs a file in a share and is refused a PUT with
      the ADR's status; an account with no access is refused a GET as the ADR says; anonymous under
      `--allow-anonymous` gets the ADR's default; with no access configured every existing test passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
