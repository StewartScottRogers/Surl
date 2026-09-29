---
id: BL-102
title: Decide curl-style help categories and --manual, and record ADR-0034
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-100, BL-101]
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: FR-009
created: 2026-09-29
completed: 2026-09-29
---
# BL-102 — Decide curl-style help categories and --manual, and record ADR-0034

## Goal

An Accepted ADR-0034, marked "Decided by Claude under Stewart's delegation", replaces
ADR-0007 section 6's single help page with curl-style help - `--help`, `--help all`,
`--help category`, `--help <category>` and `--manual` - with every text and layout pinned
from measurement of upstream curl 8.21.0, so BL-103 and BL-123 can be built without asking.

## Context

Stewart approved on 2026-09-29: help modelled on upstream curl 8.21.0's; measure the pinned
build's `--help`, `--help all`, `--help <category>` and `--manual` before pinning text.
`--help` is a short list plus a pointer to categories; `--help all`; `--help <category>`
with categories `auth`, `testing`, `security`, `tls`, `logging`, `content`, `limits`, and one
per protocol (e.g. `--help mqtt`); `--help testing` explains every loosening option and why
none is default; `--manual` holds longer text (deployment checklist, data directory,
in-memory mode).

Measured while planning, 2026-09-29, `Record-CurlExchange.ps1 -NoServer` with the pinned
reference build `C:\Program Files\Git\mingw64\bin\curl.exe` (curl 8.21.0, SHA-256
`0E7737...8778`); the ADR re-measures and records full outputs:
- `--help`: exit 0; `Usage: curl [options...] <url>`, 15 option lines
  (` -h, --help <subject>         Get help for commands` among them), a blank line, then
  `This is not the full help; this menu is split into categories.`,
  `Use "--help category" to get an overview of all categories, which are:`, the category
  names comma-separated and wrapped, `Use "--help all" to list all options`,
  `Use "--help [option]" to view documentation for a given option`.
- `--help category`: exit 0; one line per category, ` <name padded>  <description>`
  (e.g. ` auth        Authentication methods`, ` verbose     Tracing, logging etc`).
- `--help auth`: exit 0; first line `auth: Authentication methods`, then that category's
  option lines.
- `--help nosuch`: exit 0; `Unknown category provided, here is a list of all categories:`,
  a blank line, then the category list.
- `--help all`: exit 0, 274 lines, every option alphabetically.
- `--help --silent`: exit 0; the manual's paragraph for that option.
- `--manual`: exit 0, 7849 lines, an ASCII-art banner then `NAME`, `SYNOPSIS`, ...

Where the code is today: `Surl.Cli.UnitLibrary/HelpText.cs` holds one flat list (ADR-0007
section 6 plus ADR-0010 and ADR-0031 rows, description column 46);
`CommandLineOutcome.ShowHelp` carries no subject; ADR-0007 section 1 says `-h` takes no
argument and ends reading. The options ADR-0032 (BL-100) and ADR-0033 (BL-101) add must each
be placed in a category here.

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-0034-<slug>.md` exists (the next free number if
      taken; then use it in BL-103 and BL-123), Status Accepted, dated 2026-09-29 or later,
      "Decided by Claude under Stewart's delegation", citing Stewart's approval of
      2026-09-29.
- [x] It records the measured outputs listed in Context (first and last lines of each, exit
      codes, column widths, line endings) with the build path, SHA-256 and date, plus
      `--help` with a listen-URL-looking argument (`--help http://127.0.0.1:1/`) and `-h all`
      to pin how curl decides whether the next argument is a subject.
- [x] It decides and states, with the reason:
      1. The category list: each name and one-line description, in order, including
         `auth`, `testing`, `security`, `tls`, `logging`, `content`, `limits` and one per
         scheme family surl serves today (`http`, `dict`, `gopher`, `mqtt`, `telnet`,
         `tftp`), and how a protocol's category joins when its server lands.
      2. Every current and ADR-0032/ADR-0033 option's categories (an option may sit in
         several) and its one-line description; the exact `--help` short list and its
         trailing pointer lines with `surl` in place of `curl`.
      3. `--help all`, `--help category`, `--help <category>` layouts, and what an unknown
         subject gets (curl: exit 0 and the list), whether `--help <option>` is answered
         (curl 8.21.0 does) and with what text.
      4. How the parser reads the optional subject (supersedes ADR-0007 section 1's "`-h`
         takes no argument"), and the exact `CommandLineOutcome`/result shape BL-103 adds.
      5. The `--help testing` text: one paragraph per loosening option (ADR-0032) saying
         what it loosens and why it is not the default.
      6. `--manual`: its sections and their order (at least a deployment checklist, the data
         directory and `.surl` folder, in-memory mode, accounts and `--user-file`, log levels),
         where its text lives (a `Surl.Cli` resource or constant), line width, and that every
         statement must be true of the code when it lands.
- [x] It states it supersedes ADR-0007 section 6's help text and section 1's `-h` rule;
      ADR-0007 gains one "Superseded in part" line naming ADR-0034.
- [x] `Documentation/Planning/Decisions/README.md` lists ADR-0034.
- [x] `Documentation/Product/Requirements.md`: FR-009 is reworded to the categorised help
      and `--manual`, citing ADR-0034; FR-008 lists `--manual`.
- [x] No HTML comment remains in the ADR.

## Notes

Depends on BL-100 and BL-101 so every new option already has its ADR when it is placed in a
category.

2026-09-29, delivered as ADR-0034 (the number was free). Re-measured with
`Record-CurlExchange.ps1 -NoServer` on the pinned build (SHA-256 recomputed, matches).
Correction to Context: curl's `--help` has 14 option lines, not 15. Key decisions: 14
categories in ordinal order (`auth`, `content`, `dict`, `gopher`, `http`, `limits`,
`logging`, `mqtt`, `security`, `surl`, `telnet`, `testing`, `tftp`, `tls`); curl's measured
79-column layout rule adopted exactly, so every description is at most 34 characters and
defaults move to `--help <option>`, which is answered; the subject is the next argument
whatever it is (as curl), or the rest of a `-h` bundle (differs: curl shows no help for
`-hauth`); unknown option subject writes `surl: Incorrect option name ...` to stderr, exit
0; `CommandLineParseResult.ShowHelp(string? subject)` with `HelpSubject`, `HelpText.Answer`
returning `HelpAnswer(Output, Error)`, `OptionHelp` on every option row; `-M`/`--manual`
from `ManualText.cs`, 13 sections, 79 columns, no banner; the `try` line gains
`or 'surl --manual'` in BL-123. ADR-0031 and ADR-0032 also gained "Superseded in part"
lines for their help descriptions.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0034 pins curl-style --help, --help all/category/<category>/<option> and --manual from measured curl 8.21.0
