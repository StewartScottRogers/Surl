---
id: BL-003
title: Decide the Phase 1 command-line surface
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-000, BL-001, BL-024]
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-003 — Decide the Phase 1 command-line surface

## Goal

An accepted ADR gives the Phase 1 option table for `surl [options] <url>...`: every
option, how listen URLs are read, and the exact text surl prints. BL-013, BL-014,
BL-016 and BL-019 can then be implemented without asking anything.

## Context

- Product overview, "What is Surl?": the scheme picks the protocol, the host and port
  pick the bind address, and several URLs mean several listeners. "Also in scope" ends
  with a `> **TODO**` that makes the option table a Phase 1 decision recorded in an ADR.
- `Surl.Cli.UnitLibrary/CLAUDE.md`: an option keeps curl's name and meaning wherever a
  server-side meaning exists (`--cert`, `--key`, `--cacert`, `-v`, `--trace`, `-w`).
- Upstream option meanings: https://curl.se/docs/manpage.html documented curl 8.23.0 when
  checked on 2026-09-28, not the 8.21.0 reference release. Read the 8.21.0 text instead,
  either `docs/cmdline-opts/` at tag `curl-8_21_0` of https://github.com/curl/curl or
  `--help all` and `--manual` from the pinned build, recorded with
  `Record-CurlExchange.ps1 -NoServer`.
- Measure, don't assume, how upstream curl 8.21.0's own parser treats the conventions
  surl will share: short-option bundling (`-vs`), a short option's argument attached
  (`-ofile`) and separate, `--option=value`, `--` ending options, and `--no-` negation.
  Record each with `Record-CurlExchange.ps1 -NoServer` against the pinned build.
- The exit codes come from the ADR recorded by BL-001. The listen-URL type comes from the
  ADR recorded by BL-000.
- Stewart answered open question 3 on 2026-09-28: Surl is hardened for internet-facing
  use. The hardening ADR recorded by BL-024 gives the limits, defaults and exposure
  rules, and every option it says is configurable appears in this ADR's table with
  BL-024's default.
- This is a design decision delegated to Claude (root `CLAUDE.md`, "Decisions").

## Acceptance criteria

- [ ] A new ADR, numbered with the next free number, exists in
      `Documentation/Planning/Decisions/`, is marked "Decided by Claude under Stewart's
      delegation", has status Accepted, and is listed in that folder's `README.md` index.
- [ ] The ADR's option table lists every Phase 1 option: short and long name, argument,
      meaning on the server side, default, and the `SurlExitCode` for a bad value. It
      includes at least `-h`/`--help`, `-V`/`--version`, `-v`/`--verbose`, the option
      naming the served directory, and the options BL-002's TLS ADR will need
      (`--cert`, `--key`, `--cacert`), even if those are marked "parsed in Phase 1,
      served once BL-012 lands", and every limit the hardening ADR (BL-024) makes
      configurable.
- [ ] The ADR states the listen-URL rules: schemes accepted (lowercased), what `file`
      gets, the default port per scheme for every Phase 1 scheme, IPv4, bracketed IPv6
      and host-name hosts, port 0 meaning an ephemeral port, and what a path, query,
      user or password in a listen URL means or which exit code it gets.
- [ ] The ADR gives the exact text of the listener status line, the `--version` output,
      the first line of `--help`, and the stderr message for each failure, with a
      `surl: ` prefix or not as decided.
- [ ] The ADR gives the format of the `-v` verbose exchange log, including the line
      markers for bytes received, bytes sent and notes.
- [ ] The ADR records each upstream parser-convention measurement listed in Context,
      with the pinned build's SHA-256, and states which conventions surl adopts.
- [ ] `Documentation/Product/Product-Overview.md`, "Also in scope": the `> **TODO**` on
      the option table is replaced by a pointer to the new ADR.
- [ ] `Documentation/Wiki/Glossary.md` has a row for "served directory", and for any
      other term the ADR introduces, each with its name in code.

## Notes

## Log

- 2026-09-28: Created.
