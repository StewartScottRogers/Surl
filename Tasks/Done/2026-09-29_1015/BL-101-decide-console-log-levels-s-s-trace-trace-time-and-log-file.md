---
id: BL-101
title: Decide console log levels, -s, -S, --trace, --trace-time and --log-file, and record ADR-0033
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: FR-013
created: 2026-09-29
completed: 2026-09-29
---
# BL-101 — Decide console log levels, -s, -S, --trace, --trace-time and --log-file, and record ADR-0033

## Goal

An Accepted ADR-0033, marked "Decided by Claude under Stewart's delegation", fixes surl's
console log levels and every option and text that selects or shapes them, with upstream
curl 8.21.0's `--trace`, `--trace-ascii` and `--trace-time` layouts measured, so BL-104 to
BL-107 can be built without asking a question.

## Context

Stewart approved on 2026-09-29: console logging with curl-shaped levels -

| Level | Selected by | Writes |
| --- | --- | --- |
| none | `-s`/`--silent` | nothing |
| error | `-s -S` (`--show-error`) | errors only |
| info (default) | nothing | the `Listening on` lines, startup warnings, one line per connection |
| verbose | `-v` | today's exchange log (ADR-0007 section 8) |
| trace | `--trace <file>` / `--trace-ascii <file>` | every byte |

plus `--log-level <none|error|info|verbose|trace>`, `--trace-time` (timestamps) and
`--log-file <file>`. This supersedes ADR-0007's rejection of `-s`/`-S` ("Alternatives
considered") and its note that timestamps and `--trace` are later ADRs (end of section 8).

Where the code is today:
- `Surl.Cli.UnitLibrary/SurlCommandLine.cs` has `bool Verbose`; `CommandLineOptions.cs`
  lists `verbose` as a negatable flag.
- `Surl.Output.UnitLibrary/VerboseExchangeLogFactory.cs` (`VerboseExchangeLogFactory(TextWriter,
  bool verbose)`) creates a `VerboseExchangeLog` or `SilentExchangeLog`, and writes
  `NoteOutsideExchange` lines as `#- * <text>` (ADR-0028). `ListenerStatusLine` writes
  `Listening on ...` to stdout (ADR-0007 section 7).
- `Surl.Console/CommandLineRunner.cs` writes every `surl: ` error to the `error` writer and
  the throwaway-certificate note with `-v` only.
- The engine's notes (`Surl.Core.UnitLibrary/ServingEngine.cs`, ADR-0007 section 8):
  `Connection from <remote> to <local> on <listen url>`, `Flow from ...`, `Closed`,
  `Protocol server threw ...`, and the TLS handshake notes (ADR-0010 section 2).
- `-s`/`--silent` in upstream curl 8.21.0 (`curl --help --silent`, measured 2026-09-29 on the
  pinned Git for Windows build): "Do not show progress meter, warning messages or error
  messages ... Use --show-error in addition to this option to disable progress meter but
  still show error messages." `curl --help` groups them under the `verbose` category
  ("Tracing, logging etc").
- Upstream docs: https://curl.se/docs/manpage.html (`--trace`, `--trace-ascii`,
  `--trace-time`, `--trace-ids`, `--trace-config`, `-s`, `-S`, `--stderr`).

The implementation homes are already filed: BL-104 parses the options in `Surl.Cli`;
BL-105 (levels, the info line, `--trace-time`) and BL-106 (`--trace`/`--trace-ascii` dumps)
are in `Surl.Output`; BL-107 composes them in `Surl.Console`. The info line is built from
what the engine already notes (the connection note and `Closed`), so no protocol server
and no `IExchangeLog` member changes; if the ADR finds that insufficient it says so and
names the follow-up task to file.

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-0033-<slug>.md` exists (the next free number if
      taken; then use that number in BL-102 and BL-104 to BL-107), Status Accepted, dated
      2026-09-29 or later, "Decided by Claude under Stewart's delegation", citing Stewart's
      approval of 2026-09-29.
- [x] It records, with the command lines, the build (path and SHA-256 from
      `UpstreamCurlBuilds.json`) and the date, measurements made with
      `Record-CurlExchange.ps1` against a canned `200` of: `--trace <file>`,
      `--trace-ascii <file>`, `--trace <file> --trace-time`, `-v --trace-time`, and
      `--trace -` - enough lines of each output to pin its layout (header line per event,
      offset column, hex and ASCII columns, the `=> Send header`/`<= Recv header` style
      labels, timestamp format).
- [x] It decides and states, with the reason:
      1. Each level's exact output and stream (stdout or stderr): whether `-s` also hides the
         `Listening on` lines and the `surl: ` error messages, and what `-s -S` shows.
      2. `--log-level` words, case rule, error text for a bad word, and how it combines with
         `-s`, `-S`, `-v`, `--no-verbose` and `--trace` (last one given wins, or a stated
         rule).
      3. The info line per connection: exact text, when it is written (connect, close, or
         both), the datagram-flow form, and that it is built only from the engine's existing
         notes.
      4. The `--trace` and `--trace-ascii` file layout, turned to the server's side (which
         label means received and which sent, keeping ADR-0007 section 8's direction rule),
         how concurrent exchanges are kept apart (exchange id), and `-` for stdout.
      5. The `--trace-time` format (curl's `HH:MM:SS.ffffff` local time or as measured),
         taken from the injected `TimeProvider` and its local time zone, and which lines get
         it.
      6. `--log-file <file>`: what goes there instead of stderr, whether the file is
         appended or truncated, and the exit code and text when it cannot be opened
         (`CouldNotWriteFile` 23 per ADR-0031, or another ADR-0005 row).
      7. How startup warnings (ADR-0032's loosening-option lines, the throwaway-certificate
         note) are written: level, prefix, stream.
      8. Whether curl's `--trace-ids`, `--trace-config` and `--stderr` gain a server-side
         meaning now, later, or never, each with a reason.
- [x] It states which sections of ADR-0007 it supersedes (section 8's "Without `-v` it
      writes none", the timestamps note, and "Alternatives considered" on `-s`/`-S`), and
      ADR-0007 gains one "Superseded in part" line under its Status naming ADR-0033.
- [x] `Documentation/Planning/Decisions/README.md` lists ADR-0033.
- [x] `Documentation/Product/Requirements.md`: FR-013 is reworded to the decided levels
      and cites ADR-0033; FR-008 lists the new options; new rows (next free FR numbers)
      cover the log levels, `--trace`/`--trace-ascii`, `--trace-time` and `--log-file`,
      each Status Draft with its measured build.
- [x] No HTML comment remains in the ADR, and every statement in it about current code names
      a file that exists.

## Notes

Where each option appears in `--help` is ADR-0034's (BL-102).

- ADR-0033 is `Documentation/Planning/Decisions/ADR-0033-console-log-levels-trace-dumps-and-the-log-file.md`;
  the number 0033 was free, so BL-102 and BL-104 to BL-107 keep their references.
- Measured 2026-09-29 with `Record-CurlExchange.ps1` on the pinned Git for Windows 8.21.0 build:
  `--trace`, `--trace-ascii`, `--trace --trace-time`, `-v --trace-time`, `--trace -`, plus `-s`,
  `-sS`, an unopenable trace path (curl falls back to stderr silently), the override order of
  `-v`/`--trace`/`--trace-ascii`, and that `--trace` truncates. No script change was needed.
- Found drift: the engine's notes in `Surl.Core.UnitLibrary/ServingEngine.cs` are
  `Exchange <id> opened: <scheme> from <remote>.` / `Exchange <id> ended; closing the ...` etc.,
  not ADR-0007 section 8's `Connection from ...`/`Closed`. ADR-0033 restates them from the code
  and supersedes ADR-0007's list; the info line is the `Exchange <id> opened` note. BL-105's
  Context still names the old texts - ADR-0033 section 3 governs.
- Key choices (reasons in the ADR): `-s` hides `Listening on` and `surl: ` failures; command-line
  errors always written; last level option wins with no warning; `-S` lifts only `none` to
  `error`; info line at open only; dump labels `<= Recv data`/`=> Send data` with `#<id> `;
  `--log-file` appended, `--trace` truncated; an unopenable file is 23 before binding (curl's
  silent fallback rejected); `--trace-ids` and `--stderr` never, `--trace-config` later.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0033 Accepted: log levels none/error/info/verbose/trace, -s/-S/--log-level, measured --trace/--trace-ascii/--trace-time layouts, --log-file; FR-013 and FR-031 to FR-034
