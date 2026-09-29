---
id: BL-013
title: Parse listen URLs in Surl.Cli
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-003, BL-004, BL-005]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-013 — Parse listen URLs in Surl.Cli

## Goal

`Surl.Cli.UnitLibrary` turns each positional argument into the listen-URL type from
Abstractions (BL-005), or into a failure carrying the `SurlExitCode` and the stderr
message the command-line ADR (BL-003) assigns. It follows that ADR's listen-URL rules
exactly.

## Context

- The command-line ADR recorded by BL-003 under `Documentation/Planning/Decisions/`
  gives the listen-URL rules: accepted schemes, `file`, the default port per scheme,
  IPv4, bracketed IPv6 and host-name hosts, port 0, and what a path, query, user or
  password in a listen URL means. It also gives the exact error messages. Its
  `README.md` index names it.
- The listen-URL type lives in `Surl.Protocol.Abstractions.UnitLibrary` (the ADR recorded
  by BL-000, implemented by BL-005). The exit codes are in `SurlExitCode` (BL-004).
- `Surl.Cli.UnitLibrary/CLAUDE.md`: never touch the console. Return results and let the
  caller write them.
- `System.Uri` may be used, but where its behaviour differs from the ADR's rules (it
  lowercases, unescapes and fills default ports its own way), the ADR wins and a test
  pins the ADR's answer.

## Acceptance criteria

- [ ] A listen-URL parser in `Surl.Cli.UnitLibrary` returns either a listen URL or a
      failure with a `SurlExitCode` and a message.
- [ ] Data-driven fast tests in `Surl.Cli.UnitTests` cover, for each rule in the ADR,
      at least one accepted and one refused case. That includes: `http://127.0.0.1:8080/`,
      `HTTP://127.0.0.1:8080/` (scheme lowercased), `http://[::1]:8080/`,
      `http://localhost/` (default port), `http://127.0.0.1:0/`, a port above 65535, a
      non-numeric port, a missing host, `file:///tmp`, an unknown scheme such as
      `nosuch://127.0.0.1/`, and text that is not a URL.
- [ ] Every refused case asserts the exact `SurlExitCode` and message the ADR gives.
- [ ] `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean, the fast tests are
      green with no `Integration` test in `Surl.Cli.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Cli.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
