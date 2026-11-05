---
id: BL-641
title: Decide surl's configuration surface: the -K config file, repeatable item options and the one configuration model
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-611]
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md, Record-CurlExchange.ps1]
requirement: none
created: 2026-11-04
completed:
---
# BL-641 — Decide surl's configuration surface: the -K config file, repeatable item options and the one configuration model

## Goal

An ADR decides how a real server is configured: a `-K`/`--config <file>` in upstream curl's config-file syntax,
repeatable options that each add one item with a shared argument grammar, the one configuration model the
command line, the config file, `Surl.Api` and `Surl.Mcp` all fill, and how a bad item or line is refused.

## Context

- Approved by Stewart on 2026-11-04 ("Layer 2 - a configuration command line for real servers"); the design is
  Claude's under his delegation. What each mail, file, message and group option *means* to the stores is
  BL-642's ADR, which depends on this one; this ADR decides the surface they share.
- **Upstream curl's config file** is the syntax to copy. Read it at the reference release, curl 8.21.0: the
  tag `curl-8_21_0` of https://github.com/curl/curl, `docs/cmdline-opts/config.md` (and `src/tool_parsecfg.c`
  for what the text leaves unsaid). As of curl 8.23.1's https://curl.se/docs/manpage.html (checked 2026-11-04):
  one option per line; `#` as the first non-blank character is a comment; a long option may omit its dashes and
  then take `=` or `:` as separator, with dashes only whitespace separates; a parameter with whitespace or
  starting with `:`/`=` is double-quoted, inside which `\\`, `\"`, `\t`, `\n`, `\r`, `\v` are escapes and a
  backslash before any other letter is dropped; a line may be at most 10 MB (since 8.2.0); `-` reads standard
  input; `--config` may be given several times; curl also reads a default `.curlrc` unless `-q`/`--disable`.
  The manpage does not say whether `--config` nests inside a config file, or any depth limit: **measure** it
  against the pinned 8.21.0 build with `Record-CurlExchange.ps1` (a config file that includes another, and one
  that includes itself), extending the script if it cannot pass a config file (root `CLAUDE.md`: extend it, do
  not write a throwaway server). Measure the quoting and escape rows too by the request bytes curl sends for
  `header = "X: a\tb"` and the like. Record the build's path and SHA-256 from `UpstreamCurlBuilds.json`.
- **Today's command line:** `Application/Surl.Cli.UnitLibrary` (read its `CLAUDE.md`): `CommandLineOptions`
  (every row with its `OptionHelp` and `HelpCategories`), `CommandLineParser`, `OptionArgumentReader` /
  `OptionArgumentReading<T>` / `OptionArgumentType` (the `--aihelp` argument types), `SurlCommandLine` (the parsed
  result), `CommandLineAccount` and `--user` (repeats add; a name given twice refused after the whole line,
  ADR-0032 section 1), `--user-file` (ADR-0032 section 2: its line format, refusals with the line number).
  ADR-0007 (the Phase 1 surface: last value wins), ADR-0034 (help categories, 79 columns), ADR-0046 (`--aihelp`
  completeness), ADR-0005 (exit codes; a malformed command line is 2, an unreadable file 37).
- **The configuration model.** BL-611 decides the composition's input (planning: `Surl.Cli`'s `SurlCommandLine`,
  so `Surl.Api` and the command line validate identically); BL-616 maps `Surl.Api`'s options onto it, BL-668
  adds the layer-2 options to `Surl.Api`. This ADR states that the layer-2 items are part of that one model, names
  each new type (planning: records beside `CommandLineAccount`, e.g. `ConfiguredMailbox`, `ConfiguredShare`,
  `ConfiguredTopic`, `ConfiguredGroup`), and whether `Surl.Api` also accepts a config file path.
- **Secure by default** (ADR-0032, Stewart's memory "secure by default, loosen for tests"): an option that widens
  access is listed in the `security` help category and warned at every start (ADR-0032 section 9); a config file
  readable by other users is warned like `--user-file`.

Decide and record:

1. `-K`/`--config <file>`: the syntax rows above, each "as upstream curl 8.21.0" with the measurement or the
   documentation line it rests on; where an option from a config file takes effect relative to the command line
   (curl reads it in place, left to right); `-` for standard input; nesting and its depth limit (planning: allowed,
   at most 8 deep, a cycle refused); the largest file and line; whether listen URLs may appear (curl's `url =`).
2. A default config file: whether `surl` reads one (`.surlrc` and where) or none. Planning assumption: none, so
   nothing a stray file says widens access silently; if one is read, `-q` disables it as in curl.
3. The repeatable item grammar every layer-2 option shares: `<name>=<value>[;flag][;key=value]…` and comma lists,
   quoting or escaping of `;`, `,`, `=` and `:` inside a value, what an unknown key or flag does, and the
   `OptionArgumentType` it reports to `--aihelp`. The option names Stewart proposed (`--mail-domain`,
   `--mailbox <addr>:<password>[;quota=..;protocols=imap,pop3;folders=..]`, `--mail-alias <addr>=<local>,...`,
   `--mail-catch-all <domain>=<mailbox>`, `--mailbox-file <csv>`, `--shared-folder <name>=<members>`,
   `--share <name>=<path>[;readonly;protocols=…]`, `--share-access <name>=<user>:<rw|r>,...`,
   `--bucket <name>=<path>`, `--topic <filter>[;retain;qos=N]`, `--queue <name>[;durable]`,
   `--topic-map <proto>:<name>=<proto>:<name>`, `--topic-access <name>=<user>:<pub|sub>,...`,
   `--group <name>=<members>`) are proposals: confirm or refine each name; the CSV dialect of `--mailbox-file`
   (RFC 4180) and its header row.
4. Repeats, duplicates and conflicts: repeats add; the same name given twice is refused after the whole line is
   read (as `--user`); which pairs conflict.
5. Refusals: the `SurlExitCode` and exact `surl: (<code>) …` stderr text for an unreadable config file or
   `--mailbox-file`, a malformed line (naming `<file>:<line>`), a malformed item on the command line, an unknown
   option in a file, nesting past the limit; a refusal never echoes a secret (`CommandLineOption.ArgumentHoldsSecret`).
6. Bounds: config-file bytes and lines, nesting depth, items per option (hundreds of mailboxes must fit: planning
   10000), item length; each refusal past a bound.
7. Help: the new `HelpCategories` rows (planning: `mail`, `shares`, `messages`, `config`, or existing ones), so the
   `--aihelp` topic list in `AiHelpTextTests` changes knowingly, and that each category's `AiHelpExamples` entry
   lands with the task that makes its behaviour true.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation", citing Stewart's approval of 2026-11-04 and BL-611's ADR. It answers
      decisions 1 to 7; every config-file syntax row cites `config.md` at `curl-8_21_0` or a measurement of the
      pinned 8.21.0 build (path and SHA-256 named), and is listed in `Documentation/Planning/Decisions/README.md`.
- [ ] Each measurement is reproducible: the ADR gives the `Record-CurlExchange.ps1` command line, and if the
      script was extended, its change is in the same commit and its header documents the new parameter.
- [ ] The ADR names every option, its argument type, help category, default, and whether it widens access.
- [ ] `Documentation/Product/Requirements.md` has a new functional requirement row (next free FR number) for the
      configuration command line and config file, citing the ADR, marked Draft, naming the build tasks.
- [ ] The ADR's Consequences section lists the build tasks by ID (BL-644, BL-645, BL-646, BL-653, BL-654, BL-663,
      BL-666, BL-668) and names any whose Goal or option names it changes.

## Notes

## Log

- 2026-11-04: Created.
