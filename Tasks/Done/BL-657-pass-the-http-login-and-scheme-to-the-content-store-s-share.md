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
completed: 2026-11-05
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

- [x] Named tests replay requests where: an account with `r` access GETs a file in a share and is refused a PUT with
      the ADR's status; an account with no access is refused a GET as the ADR says; anonymous under
      `--allow-anonymous` gets the ADR's default; with no access configured every existing test passes unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Statuses come from ADR-0102 decision 2.3's table, already measured in ADR-0008: a read in a share
  the caller may not read is the empty `404` a missing path gets (indistinguishable, ADR-0006
  section 3); a write is the `405 Allow: GET, HEAD` every `PUT` gets, because the HTTP server takes
  no uploads. No new byte reaches upstream curl, so no new measurement or ADR was needed.
- Change is all in `HttpRequestAnswerer`: the let-in verdict's `AccountName` and
  `ExchangeContext.Scheme` become a `ContentCaller`, and the lookup is `MapRequestPathForCaller`.
  A body-bound login (ADR-0045) uses the account of the body check's final verdict. HTTP/2 and
  HTTP/3 reuse the answerer, so they are covered with no change. `Surl.HttpServing` untouched.
- Default chosen: an empty `AccountName` is treated as anonymous rather than throwing from
  `ContentCaller.ForAccount`, so no verdict can crash the exchange.
- Passing the scheme (the callerless lookup passed none) means a share limited by `protocols=http`
  is now visible to the HTTP server; a scheme's TLS twin is matched by whatever `Schemes` the
  composition hands the share (ADR-0102 decision 4.3), not here.
- Tests: `HttpRequestAnswererTests.ShareAccess.cs`, 10 tests (r GET 200, r PUT 405, no grant 404
  identical to a missing path, anonymous 404 on a closed share, `@everyone` 200, no access rows 200,
  empty account anonymous, `protocols=` own/other scheme, body-bound login). Fast run: 0 failing
  projects; HTTP tests 461; `Surl.Protocol.Http.UnitLibrary` 100% line, 100% branch, worst CRAP 10.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The HTTP server passes each request's login and scheme to the content store: unreadable shares answer 404, writes stay 405
