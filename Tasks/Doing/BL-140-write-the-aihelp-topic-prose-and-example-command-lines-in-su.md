---
id: BL-140
title: Write the --aihelp topic prose and example command lines in Surl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-139]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: FR-035
created: 2026-09-29
completed:
---
# BL-140 — Write the --aihelp topic prose and example command lines in Surl.Cli

## Goal

Every `--aihelp` topic and the overview carry their hand-written sections - what the topic
covers in depth and the exact example command lines with the exact lines surl writes back -
in the source constant BL-137's ADR names, each statement true of the code on the day it
lands.

## Context

- Decision: BL-137's ADR (expected ADR-0046), decisions 3, 4, 6 and 7 (which examples each
  topic shows and the exact output each one pins).
- What the text must cover (Stewart, 2026-09-29): what surl is and how a command line is
  built (overview); how to read the bound port from `Listening on <scheme>://<host>:<port>/`
  when the listen URL has port 0 (ADR-0007 section 7, FR-006); in-memory versus
  `--directory` and the `.surl` folder and lock, exit 124 (ADR-0031, FR-022 to FR-025);
  accounts, `--user-file` and the login rules (ADR-0032, FR-026 to FR-030); the loosening
  options are for tests only and each writes a `surl: warning:` line at start (ADR-0032
  section 9, ADR-0034 decision 5); log levels, `--trace`, `--log-file` (ADR-0033); the
  limits and their defaults (ADR-0006 section 1, NFR-008 to NFR-016); per protocol, the
  schemes it claims, their default ports (`Surl.Cli.UnitLibrary/SchemeDefaultPorts.cs`), what
  it serves and a pinned upstream curl 8.21.0 command line that reaches it (for example
  `curl mqtt://127.0.0.1:<port>/<topic>`).
- Truth: check each sentence against the code (`Surl.Console/CommandLineRunner.cs`,
  `ListenerStartReporter.cs`, `DataDirectoryLock.cs`, the protocol libraries), not against
  an ADR. `ManualText.cs` already holds checked text for several of these subjects; reuse
  its facts, and where `--aihelp` and `--manual` say the same thing, keep the wording the same.
  Describe only behaviour that exists; the task that later changes it updates the text.
- Any curl command line shown is one pinned upstream curl 8.21.0 accepts, per
  https://curl.se/docs/manpage.html; never the Curl port (ADR-0003).
- Proof that each shown surl output is what `surl` writes is BL-142's; here the examples are
  data the generator places, exposed so BL-142 can run them (the ADR's decision 7 shape).

## Acceptance criteria

- [ ] Every topic page and the overview have every hand-written section the ADR prescribes
      filled; no page still holds the ADR's "nothing here" text for a section the ADR
      requires to be written.
- [ ] Each example the ADR's decision 7 lists appears exactly as the ADR gives it, in the
      topic it names, and is exposed in the shape BL-142 needs.
- [ ] A test in `Surl.Cli.UnitTests` asserts every `--option` named anywhere in the prose or
      examples exists in `CommandLineOptions.All` (as `ManualTextTests` does for the manual),
      and every `surl:` refusal text quoted matches the constant or format the code uses.
- [ ] The pinned full outputs in BL-139's tests are updated to the written text and pass.
- [ ] Notes lists each statement checked and the file it was checked against.
- [ ] `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test
      Surl.Cli.UnitTests --filter "TestCategory!=Integration"` passes; `Surl.Cli.UnitLibrary`
      stays at 100% line and branch coverage.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
