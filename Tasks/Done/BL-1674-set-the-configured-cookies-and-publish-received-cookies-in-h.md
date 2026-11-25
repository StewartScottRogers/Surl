---
id: BL-1674
title: Set the configured cookies and publish received cookies in Http3ProtocolServer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: FR-015
created: 2026-11-24
completed: 2026-11-24
---
# BL-1674 — Set the configured cookies and publish received cookies in Http3ProtocolServer

## Goal

`Http3ProtocolServer` adds the configured `Set-Cookie` fields to HTTP/3 replies and publishes a `CookieReceivedEvent` for each cookie a request carries, as `HttpProtocolServer` does.

## Context

ADR-0189 decision 2 says every reply on every HTTP or HTTPS listener. BL-1660 wired `CookieHandler` into `HttpProtocolServer` only (HTTP/1.x, HTTP/2, h2c). The library's `CLAUDE.md` Cookies paragraph also still says `HttpProtocolServer` has no option reader (BL-1661); BL-1661 and BL-1673 are done, so correct it.

## Acceptance criteria

- [x] A named test in `Surl.Protocol.Http.UnitTests` shows a configured cookie's `set-cookie` field on an HTTP/3 reply.
- [x] A named test shows an HTTP/3 request's `cookie` field published as `CookieReceivedEvent`s.
- [x] A named test shows that with no cookie configured the HTTP/3 reply is unchanged.
- [x] The library's `CLAUDE.md` Cookies paragraph is true of the code.
- [x] Build clean and fast tests green.

## Notes

- `Http3ProtocolServer` takes `cookies` as an optional last constructor argument (null or none sets
  none), so `Surl.Composition` builds unchanged until BL-1679 passes them. `Http3ApplicationProtocol`
  wraps its answerer in the same `CookieHandler` the TCP server uses. No new behaviour decision
  (ADR-0189 decision 2 already covers it), so no new ADR.
- Done in the session without the `/feature` subagents: about ten lines of production code reusing
  `CookieHandler`.
- Tests in `Http3ProtocolServerTests`:
  `ServeAsync_ConfiguredCookies_AddEachSetCookieFieldToTheHttp3Reply`,
  `ServeAsync_RequestCookieFields_PublishesEachPairAsCookieReceivedEvents`,
  `ServeAsync_NoCookieConfigured_Http3ReplyIsUnchanged`.
- Verify: build 0 warnings, 0 errors; fast tests green (`Surl.Protocol.Http.UnitTests` 841 passed).
  `dotnet format --verify-no-changes` passes on the changed files; it reports line-ending and import
  ordering errors in files this task did not touch (BL-1678 covers the import ordering and says
  the line endings are a worktree artifact).
- No live upstream curl run: `surl --http3` does not pass cookies to this server until BL-1679.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Http3ProtocolServer sets the configured cookies on HTTP/3 replies and publishes the cookies requests carry
