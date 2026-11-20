---
id: BL-1439
title: Start a Surl.Api server whose only listen URLs come from its ConfigFiles
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1438]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1439 — Start a Surl.Api server whose only listen URLs come from its ConfigFiles

## Goal

`await SurlServer.StartAsync(new SurlServerOptions { ConfigFiles = { "fixture.conf" } })`, with the
file holding `url http://127.0.0.1:0/`, starts and lists that URL in `BoundUrls`, as `surl -K fixture.conf`
does.

## Context

- Surly bug report `2026-11-19T1942-Surl-bug-report.md` item 3 (Surl `763754785`, unchanged since).
- Cause: `Application/Surl.Api/SurlServerArguments.cs:25-28` throws
  `ArgumentException("ListenUrls names no listen URL.")` before the config file is read.
- BL-1438's amendment to ADR-0099 fixes when an empty `ListenUrls` is still misuse; the parser's own
  `no URL specified` (exit 2) covers the rest, thrown as `SurlServerStartException`.
- Tests run with no disk: config files are given in memory through `ApiTestSeams.StartFiles`
  (`SurlServerHostSeams.ReadStartFile`, `ApiConfigFileSource`).

## Acceptance criteria

- [ ] A test in `Application/Surl.Api.UnitTests` starts with only `ConfigFiles` (an in-memory file with
      `url http://127.0.0.1:0/`) and asserts `BoundUrls` holds `http://127.0.0.1:49731/`
      (`FakeListenerFactory.BoundPort`).
- [ ] A start whose config file names no URL throws `SurlServerStartException` with
      `SurlExitCode.FailedInit` and the line `(2) no URL specified`, or whatever the amendment says
      (named test).
- [ ] The cases BL-1438 keeps as `ArgumentException` still throw it (named test), and
      `SurlServerOptions.ListenUrls`' doc comment no longer says "At least one is needed" without the
      amendment's condition.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and coverage
      stays at 100% line and branch.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T1942-Surl-bug-report.md` item 3.
