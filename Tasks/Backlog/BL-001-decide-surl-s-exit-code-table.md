---
id: BL-001
title: Decide Surl's exit-code table
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-001 — Decide Surl's exit-code table

## Goal

An accepted ADR lists every way the `surl` process can fail in Phase 1, with the
`SurlExitCode` name and number each one returns. BL-004 can then add them to the enum,
and the command-line and serving tasks can name the code for each failure path.

## Context

- `Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs` holds `Ok = 0` and
  `FailedInit = 2` today. Its remarks say Phase 1 decides the full table in an ADR,
  reusing upstream curl's `CURLE_*` number wherever a server-side meaning carries over,
  and that a value is never renumbered.
- Upstream numbers, from https://curl.se/libcurl/c/libcurl-errors.html, checked
  2026-09-28. The page states no version, and these numbers are stable across releases:
  `CURLE_OK` 0, `CURLE_UNSUPPORTED_PROTOCOL` 1, `CURLE_FAILED_INIT` 2,
  `CURLE_URL_MALFORMAT` 3, `CURLE_COULDNT_CONNECT` 7, `CURLE_READ_ERROR` 26,
  `CURLE_OPERATION_TIMEDOUT` 28, `CURLE_BAD_FUNCTION_ARGUMENT` 43,
  `CURLE_SEND_ERROR` 55, `CURLE_RECV_ERROR` 56, `CURLE_SSL_CERTPROBLEM` 58. Read the
  whole page for a better fit before choosing. Candidates are not decisions.
- What upstream curl's own command line returns for its own bad command lines is a
  measurement, not a guess. Run the pinned upstream curl 8.21.0 build
  (`UpstreamCurlBuilds.json`) with
  `Record-CurlExchange.ps1 -NoServer -CurlArgs … -OutDirectory …` for at least: an
  unknown long option, an option missing its argument, a malformed URL, and a URL with a
  scheme curl does not support. Where surl's command line fails the same way, surl
  returns the same number.
- This is a behaviour decision delegated to Claude (root `CLAUDE.md`, "Decisions").

## Acceptance criteria

- [ ] A new ADR, numbered with the next free number, exists in
      `Documentation/Planning/Decisions/`, is marked "Decided by Claude under Stewart's
      delegation", has status Accepted, and is listed in that folder's `README.md` index.
- [ ] The ADR has a table with one row per failure. Each row gives the `SurlExitCode`
      member name, its number, the upstream `CURLE_*` name it reuses (or "none", with
      the reason no upstream meaning carries over), and the failure it covers. The rows
      cover at least: success, unknown option, option missing its argument, invalid
      option value, malformed listen URL, a scheme Surl has no server for, the served
      directory missing or unreadable, a listen address that cannot be bound (in use,
      not local, not permitted), and an unexpected internal failure.
- [ ] The ADR states the exit code when surl is stopped by Ctrl+C or SIGTERM after
      serving normally.
- [ ] The ADR records, for each of the four upstream measurements above, the command
      line, the exit code and the first line of stderr the pinned 8.21.0 build
      produced, with the build's SHA-256.
- [ ] `Ok = 0` and `FailedInit = 2` keep their numbers. No existing value is renumbered.
- [ ] `Documentation/Wiki/Glossary.md`, row "exit code", points to the new ADR.

## Notes

The command-line ADR (BL-003) cites this table for every bad-value path, and BL-004 turns
it into enum members.

## Log

- 2026-09-28: Created.
