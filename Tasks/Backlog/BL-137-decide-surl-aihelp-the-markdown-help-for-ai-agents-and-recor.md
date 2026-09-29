---
id: BL-137
title: Decide surl --aihelp, the Markdown help for AI agents, and record its ADR
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: FR-035
created: 2026-09-29
completed:
---
# BL-137 — Decide surl --aihelp, the Markdown help for AI agents, and record its ADR

## Goal

An Accepted ADR (the next free number, expected ADR-0046), marked "Decided by Claude under
Stewart's delegation", pins `surl --aihelp [topic]` - a second help system beside `--help`,
written in Markdown for an AI agent learning to call the surl CLI - so BL-138 to BL-142 can be
built without asking a question.

## Context

Stewart approved the feature on 2026-09-29. Summary of his request:

- `surl --aihelp`: an overview - what surl is, how a command line is built, the topic list.
  `surl --aihelp <topic>`: one topic in depth. `surl --aihelp all`: everything in one
  document. It is a help system for an agent to use surl outright or wrap it in a skill; it
  is **not** a skill-file generator.
- Topics: at least `auth`, `testing`, `logging`, `content`, `limits`, `exit-codes` and one per
  registered protocol (e.g. `mqtt`); aligned with ADR-0034 decision 1's `--help` categories
  (`auth`, `content`, `dict`, `gopher`, `http`, `limits`, `logging`, `mqtt`, `security`, `surl`,
  `telnet`, `testing`, `tftp`, `tls`), plus `exit-codes` and anything else an agent needs.
- Output: Markdown only (Stewart's choice; no JSON), precise and complete, one stable
  structure identical on every topic, no colour, no paging, stdout, exit 0.
- Every option with its argument type, default, allowed values, and whether it loosens
  security (the secure-by-default rule: loosening options are for tests only). Exact example
  command lines and what surl prints back (the `Listening on` line and reading the bound port
  for port 0; in-memory versus `--directory`; the data-directory lock). Every exit code with
  its meaning and what an agent should do next.
- Generated from the option table and categorised help `--help` uses, so the two cannot
  drift; tests fail when an option, a topic, a registered protocol or an exit code is missing.
- An unknown topic: decided here, mirroring how `--help <unknown>` answers (ADR-0034
  decision 3: the unknown-category line and the category list, stdout, exit 0).
- `--aihelp` appears in `--help`, `--help all` and its category.

There is no upstream curl equivalent. Measured while planning, 2026-09-29,
`Record-CurlExchange.ps1 -NoServer -CurlArgs "--aihelp"` with the pinned reference build
`C:\Program Files\Git\mingw64\bin\curl.exe` (curl 8.21.0, SHA-256 `0E7737...8778` per
`UpstreamCurlBuilds.json`): exit 2, stdout empty, stderr `curl: option --aihelp: is unknown`
then `curl: try 'curl --help' or 'curl --manual' for more information`. curl 8.21.0's manual
(https://curl.se/docs/manpage.html) has no such option. Re-measure and record it in the ADR.

Where the code is on 2026-09-29, all in `Surl.Cli.UnitLibrary` unless named:

- `CommandLineOptions.cs`: the option table; each `CommandLineOption` row carries
  `OptionHelp(ArgumentName, Description, Categories, IsInShortList, Default, Explanation)`
  (`OptionHelp.cs`). Arguments are read by `OptionArgumentReader` (`ReadSeconds`, `ReadNumber`,
  `ReadBytes`, `ReadPath`, `ReadTlsVersion`, `ReadCertificateType`, `ReadKeyType`,
  `ReadLogLevel`, `ReadText`, `ReadAccount`, `ReadAuthenticationMethods`) - the source of truth
  for each argument type and its allowed values.
- `HelpCategories.cs` / `HelpCategory.cs`: `HelpCategory(Name, Description)`, no scheme list.
- `HelpText.cs`: `HelpText.Answer(string? subject)` returns `HelpAnswer(Output, Error)`.
  `HelpLayout.cs` holds the column and wrapping rules. `ManualText.cs` holds `--manual`,
  including its `EXIT CODES` section.
- `CommandLineParser.cs`, `CommandLineParseResult.cs` (`ShowHelp(string? subject)`,
  `HelpSubject`), `CommandLineOutcome.cs` (`Serve`, `ShowHelp`, `ShowManual`, `ShowVersion`,
  `Refused`), `CommandLineOption.cs` (`CommandLineOptionKind`: `Help`, `Version`, `Manual`,
  `Flag`, `Argument`).
- `SurlExitCode` is in `Surl.Protocol.Abstractions.UnitLibrary/SurlExitCode.cs` (0, 1, 2, 3,
  6, 23, 37, 45, 58, 77, 124, 125); `Surl.Cli.UnitLibrary` already references that project.
- `Surl.Console/CommandLineRunner.cs` composes the registered servers in
  `ComposeProtocolServers` (schemes `http`, `https`, `dict`, `gopher`, `gophers`, `mqtt`,
  `mqtts`, `telnet`, `tftp`) and switches on `CommandLineOutcome`; `Surl.Cli` does not know
  which servers are registered.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-00NN-<slug>.md` exists (the next free number;
      BL-138 to BL-143 then use it), Status Accepted, dated 2026-09-29 or later, "Decided by
      Claude under Stewart's delegation", citing Stewart's approval of 2026-09-29, and
      stating that `--aihelp` is a deliberate addition with no upstream curl equivalent, with
      the measured curl 8.21.0 answer above (build path, SHA-256, date, exit code, stderr).
- [ ] It decides and states, each with its reason:
      1. **Parsing:** the option's long name `aihelp`, whether it has a short name, how the
         optional topic is read (`--aihelp=<topic>`, the next argument, empty topic), whether
         reading ends once the topic is taken, `--no-aihelp`, precedence against `-h`, `-V`
         and `-M` - mirroring ADR-0034 decision 4 unless a reason is given - and the result
         shape: the new `CommandLineOutcome` member, the `CommandLineParseResult` factory and
         subject property, the new `CommandLineOptionKind` member.
      2. **Its `--help` row:** the left side, a description of at most 34 characters (ADR-0034
         decision 2), its categories (at least `surl`), `IsInShortList` true, its default, and
         the re-pinned `--help` short list and `--help surl` page.
      3. **The topics:** the exact topic names in their order - every ADR-0034 category, plus
         `exit-codes`, plus any other topic an agent needs (for example listen URLs and
         ports) - and the rule that a protocol category is a protocol topic, so a protocol
         server's registering task adds its topic in the same change it adds its category.
         Topic matching (case, as ADR-0034 decision 3 point 3).
      4. **The one Markdown structure:** the exact heading skeleton every topic page follows
         (title line, the fixed `##` sections in a fixed order, what a section says when it
         has nothing - never omitted), the overview page, and `all` (the overview then every
         topic in order, headings demoted or not). Line endings (`Environment.NewLine` after
         every line, as ADR-0034 decision 3), no trailing spaces, no ANSI escape, no wrap
         width or the width chosen, how `|` and backticks inside a table cell are escaped.
      5. **The option table columns:** option (short and long name), argument type, default,
         allowed values, loosens security, categories, description; the fixed vocabulary for
         argument type (derived from which `OptionArgumentReader` method reads the option) and
         for allowed values (numbers with their ranges and 0 meaning no limit, byte suffixes,
         the `--log-level` words, `--tls-max` versions, `--cert-type`/`--key-type` formats,
         `--auth` words); and which options "loosen security" and with what wording - at
         least the four `testing` options (`--allow-anonymous`, `--allow-plaintext-auth`,
         `--auth`, `--self-signed`) marked for tests only, and whether the other `security`
         options (`--allow-uploads`, `--list-directories`, `--follow-symlinks`,
         `--serve-dot-files`, `--tlsv1.0`, `--tlsv1.1`) are marked differently.
      6. **Where each fact lives** so `--help` and `--aihelp` share one source: the fields
         `OptionHelp` gains (or how each column is derived from existing fields), whether
         `HelpCategory` gains its schemes, the exit-code guidance table's type and file in
         `Surl.Cli.UnitLibrary` (one row per `SurlExitCode` member: number, name, meaning,
         what an agent should do next), and where each topic's hand-written prose and examples
         live (a source constant, as `ManualText.cs`, not a resource).
      7. **Examples:** which exact command lines each topic shows and the exact lines surl
         writes back for each (at least: `surl http://127.0.0.1:0/` and its
         `Listening on http://127.0.0.1:<port>/` line and how to read the bound port; serving
         in memory versus `--directory <path>`; the `(124)` refusal when another surl holds the
         data directory), and how a test proves each shown output is what `surl` writes (the
         `Surl.Console.UnitTests` fakes, `FakeListenerFactory`).
      8. **Unknown topic:** its exact output, stream and exit code, mirroring `--help
         <unknown>`, and what an option-like topic (`--aihelp --user`) gets.
      9. **Completeness tests:** the named tests that fail when an option, a topic, a
         registered protocol scheme or a `SurlExitCode` member is missing from `--aihelp`,
         and which project each lives in (`Surl.Cli.UnitTests` for options, topics and exit
         codes; `Surl.Console.UnitTests` for registered schemes and examples).
      10. Whether `--manual`'s `SEE ALSO` names `surl --aihelp`.
- [ ] It says which later tasks build it: BL-138 (facts), BL-139 (generator), BL-140
      (prose and examples), BL-141 (parsing, `--help` row and wiring), BL-142 (registered
      schemes and examples proved), BL-143 (documents).
- [ ] ADR-0034 gains a "Superseded in part" or "Extended by" line naming the new ADR for its
      option table and short list.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [ ] `Documentation/Product/Requirements.md` gains FR-035 for `--aihelp` (overview, topics,
      `all`, Markdown on stdout with `Ok` (0), generated from the option table, citing the
      new ADR, upstream column "none: no curl 8.21.0 equivalent, measured"), and FR-008 lists
      `--aihelp`.
- [ ] No HTML comment remains in the ADR; no `.cs` file changes (`git diff --stat`).

## Notes

## Log

- 2026-09-29: Created.
