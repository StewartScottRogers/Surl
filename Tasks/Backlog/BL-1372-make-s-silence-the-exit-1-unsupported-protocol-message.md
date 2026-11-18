---
id: BL-1372
title: Make -s silence the exit-1 unsupported-protocol message
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-18
completed:
---
# BL-1372 — Make -s silence the exit-1 unsupported-protocol message

## Goal

`surl -s nosuch://127.0.0.1:0/` and `surl --log-level none nosuch://127.0.0.1:0/` write nothing on stderr and exit 1,
as upstream `curl -s nosuch://x` does and as `surl --aihelp exit-codes` already promises.

## Context

- From the Surly cookbook bug report of 2026-11-18 (finding 1), reproduced at commit `3c5b39474`.
- Today both commands print `surl: (1) Protocol "nosuch" not supported` and exit 1; `file://` (when not served) too.
  Other start failures (exits 23, 45, 58, 124) are already silenced by `-s`, e.g. `surl -s https://127.0.0.1:0/`
  without `--cert` prints nothing and exits 58.
- `surl --aihelp exit-codes`: "With `-s` and without `-S`, a failure found once the command line is read writes
  nothing, and only the exit code says what failed; a refused command line is always written."
- The message comes from `Application/Surl.Cli.UnitLibrary/ListenUrlParser.cs:118` (and `:137` for the stdio
  scheme) as a `ListenUrlParseResult.Refused`, so it is treated as a refused command line. Upstream curl treats an
  unsupported scheme as a transfer failure (exit 1, `CURLE_UNSUPPORTED_PROTOCOL`), silenced by `-s` and shown again
  by `-S`. Decided by Claude: follow upstream curl - silence it.
- `AiHelpExamples.cs:27` and `:375` and `AiHelpProse.cs:150,205,307` describe this message; keep them true (the
  examples run without `-s`, so their text may stay; check
  `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`).

## Acceptance criteria

- [ ] A named fast test runs `-s nosuch://127.0.0.1:0/` and asserts exit 1 (`SurlExitCode.UnsupportedProtocol`) and empty stderr.
- [ ] A named fast test runs `--log-level none nosuch://127.0.0.1:0/` and asserts exit 1 and empty stderr.
- [ ] A named fast test runs `-s -S nosuch://127.0.0.1:0/` and asserts stderr is `surl: (1) Protocol "nosuch" not supported`.
- [ ] Without `-s` the message and exit 1 are unchanged (existing tests still pass).
- [ ] `--aihelp exit-codes` (and any other prose naming this case) states that an unsupported scheme is silenced by `-s`.
- [ ] `dotnet build` is clean and the fast tests are green; Surl.Cli and Surl.Console stay at 100/100 coverage.

## Notes

- Find where `-s`/`--log-level none` decide what a refused parse writes; the fix is likely to classify exit 1 from
  the listen-URL parser as a post-command-line failure, not to special-case the string.

## Log

- 2026-11-18: Created.
