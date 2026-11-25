---
id: BL-1679
title: Pass the configured cookies to the composed HTTP/3 server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1673, BL-1674]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Api, Documentation/Product/Requirements.md]
requirement: FR-015
created: 2026-11-24
completed: 2026-11-25
---
# BL-1679 — Pass the configured cookies to the composed HTTP/3 server

## Goal

`surl --http3 --set-cookie` sets the cookie on HTTP/3 replies too.

## Context

ADR-0189 decision 2. `SurlServerComposition.ComposeHttp3ProtocolServer` (`SurlServerComposition.Start.cs`) builds the `Http3ProtocolServer` without `SurlCommandLine.Cookies`; BL-1674 gives that server a way to take them. Update the library's `CLAUDE.md` sentence saying the HTTP/3 server does not set cookies.

## Acceptance criteria

- [x] A composition test with `--http3` and one cookie sees its `set-cookie` field in an HTTP/3 reply.
- [x] With no `--set-cookie` the HTTP/3 reply is unchanged.
- [x] The library's `CLAUDE.md` is true of the code.
- [x] Build clean and fast tests green.

## Notes

- Fix: `ComposeHttp3ProtocolServer` passes `commandLine.Cookies`. Tests:
  `ComposeAsync_Http3WithASetCookie_AddsItsSetCookieFieldToTheReply` (failed before the fix) and
  `ComposeAsync_Http3WithNoSetCookie_RepliesAsWithOneLessItsSetCookieField`.
- The HTTP/3 tests compare every reply field but `date`: with a clock that stands still the QUIC connection never
  leaves its closing state and the test hangs.
- `touches` widened (no task in Doing names them; BL-1687 holds Surl.Mcp only): `Surl.Cli.UnitLibrary` (`--aihelp`
  said HTTP/3 sets no cookie), `Surl.Api` (docs said HTTP/1.x and HTTP/2 only) and `Requirements.md` (FR-015 said
  unbuilt).
- Pipeline: a one-argument change, so plan, tests, implementation and docs were done in the session; the review
  stage went to `code-reviewer` (no must-fix; its `Surl.Api` doc findings are fixed). No ADR: ADR-0189 decision 2
  already decides it.
- Checked by hand with pinned static-curl 8.21.0 (hash verified) over `--http3-only`: the reply carried
  `set-cookie: session=t1; Path=/; HttpOnly`, `-c` stored it and `-b` sent it back. The repeatable test is BL-1689.
- `dotnet format --verify-no-changes` passes on the changed `.cs` files; it fails on files this task did not change
  (already filed: BL-1678, BL-1686).

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. surl --http3 --set-cookie sets the cookie on HTTP/3 replies too; pinned curl 8.21.0 stores and returns it (checked by hand).
