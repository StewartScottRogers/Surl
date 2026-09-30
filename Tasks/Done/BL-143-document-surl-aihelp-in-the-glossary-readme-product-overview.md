---
id: BL-143
title: Document surl --aihelp in the glossary, README, product overview and CLAUDE.md files
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-124, BL-142]
touches: [Documentation/Wiki, README.md, Documentation/Product/Product-Overview.md, CLAUDE.md, Surl.Cli.UnitLibrary/CLAUDE.md, Surl.Console/CLAUDE.md]
requirement: FR-035
created: 2026-09-29
completed: 2026-09-29
---
# BL-143 — Document surl --aihelp in the glossary, README, product overview and CLAUDE.md files

## Goal

The glossary, the root README, the product overview, the root `CLAUDE.md` and the
`CLAUDE.md` files of `Surl.Cli.UnitLibrary` and `Surl.Console` state, truly of the code as
it then is, that `surl --aihelp [topic]` is the Markdown help for AI agents, how it is
generated from the option table, and what a change must keep in step.

## Context

- Decision: BL-137's ADR (expected ADR-0046); behaviour landed in BL-138 to BL-142.
- Depends on BL-124, which documents `--help`, `--manual`, accounts and log levels in the
  same files; build on its wording, one concept one name (`align-and-document`).
- `Documentation/Wiki/Glossary.md`: the term the code uses for `--aihelp` output and for a
  topic (as named by the ADR and the types BL-139 added), distinct from a `--help` category.
- `README.md`: one short section - for an AI agent, run `surl --aihelp`, then
  `surl --aihelp <topic>` or `surl --aihelp all` - with command lines that run as written.
- `Documentation/Product/Product-Overview.md`: where it lists the command-line help, add
  `--aihelp` as a deliberate addition with no upstream curl equivalent.
- Root `CLAUDE.md`: under "Solution-wide conventions" or "Overview", one line that an agent
  learns the CLI with `surl --aihelp`, and that adding an option, a protocol server or a
  `SurlExitCode` member is not finished until its `--aihelp` facts and topic exist (the
  completeness tests of BL-139 and BL-142 fail otherwise).
- `Surl.Cli.UnitLibrary/CLAUDE.md` and `Surl.Console/CLAUDE.md`: what each project now holds
  for `--aihelp`, naming the real types and files.

## Acceptance criteria

- [x] `Documentation/Wiki/Glossary.md` defines the `--aihelp` term(s) once, with the names
      the code uses (`rg` each type name the entry cites finds it in `Surl.Cli.UnitLibrary`).
- [x] `README.md` has the section above, and every command line in it runs as written
      (`dotnet run --project Surl.Console -- --aihelp` and each other one, exit 0).
- [x] The product overview and the three `CLAUDE.md` files name only types, options and files
      that exist (`rg` each name) and state the rule that options, protocol servers and exit
      codes keep `--aihelp` complete.
- [x] No behaviour change: `git diff --stat` shows only `*.md` files; `dotnet build` and
      `dotnet test --filter "TestCategory!=Integration"` still pass.

## Notes

- 2026-09-29: Delivered by `align-and-document`. The glossary gains "AI help", "AI help topic",
  "AI help example", "argument type" and "exit-code guidance", and "help category" now says
  each category is also an AI help topic. The README gains "Help for AI agents", and each of
  its five command lines (`--aihelp`, `listen-urls`, `http`, `exit-codes`, `all`) ran through
  `dotnet run --project Surl.Console` and exited 0.
- Decision: the root `CLAUDE.md` rule names only the completeness tests that actually fail.
  An option's help category is left out because no test enforces it. The rule adds
  `AiHelpTextTests.Topics_AreTheAdrsSixteenInOrdinalOrder`, which pins the topic list, so a
  new protocol topic breaks it too.
- Checks: every type, member, file and test the documents name was found with `rg`.
  `git diff --stat` shows only the six `*.md` files. The build is clean, and the fast tests
  pass with 0 failures across 16 test projects.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl --aihelp is documented in the glossary, README, product overview and the three CLAUDE.md files, with the completeness rule for options, protocol servers and exit codes
