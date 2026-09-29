---
id: BL-014
title: Parse the Phase 1 options in Surl.Cli
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-013]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-014 — Parse the Phase 1 options in Surl.Cli

## Goal

`Surl.Cli.UnitLibrary` parses a whole `surl` command line into one immutable
parsed-command-line record: every option in the command-line ADR's (BL-003) Phase 1
table, plus the listen URLs parsed by BL-013. Failures carry the exact `SurlExitCode` and
message the ADR gives. The library also produces the `--help` and `--version` text the
ADR specifies.

## Context

- The command-line ADR recorded by BL-003 under `Documentation/Planning/Decisions/` is
  the specification. It gives the option table, the parser conventions (bundling,
  attached arguments, `--option=value`, `--`, `--no-`, as measured from upstream curl
  8.21.0), the `--help` and `--version` text, and the error messages. Its `README.md`
  index names it.
- The listen-URL parser from BL-013 handles the positional arguments.
- `Surl.Cli.UnitLibrary/CLAUDE.md`: never touch the console. `Surl.Console` hands this
  library its arguments and its writers.
- Cyclomatic complexity is at most 10 per method (`CodeMetricsConfig.txt`, `CA1502`,
  enforced at build). A table-driven option parser keeps a growing table from breaking
  the build.
- `--help` and `--version` are results that tell the caller to print text and exit with
  `SurlExitCode.Ok`. They do not serve.

## Acceptance criteria

- [ ] A command-line parser in `Surl.Cli.UnitLibrary` returns a parsed-command-line
      record (listen URLs and every Phase 1 option value, defaults applied) or a
      failure with a `SurlExitCode` and message.
- [ ] Fast tests cover every row of the ADR's option table: the option accepted in each
      form the ADR adopts, and its bad-value case asserting the exact exit code and
      message.
- [ ] Fast tests cover: an unknown option, an option missing its argument, no listen
      URL at all, several listen URLs in order, `--` followed by an argument that starts
      with `-`, and `--help` and `--version` returning the ADR's exact text with
      `SurlExitCode.Ok`.
- [ ] No method in `Surl.Cli.UnitLibrary` exceeds complexity 10:
      `dotnet build Surl.Cli.UnitLibrary -warnaserror` is clean.
- [ ] The fast tests are green, and `Measure-CodeQuality.ps1` reports no failing member
      in `Surl.Cli.UnitLibrary`.

## Notes

The options BL-003 marks "parsed in Phase 1, served once BL-012 lands" (`--cert`,
`--key`, `--cacert`) are parsed and validated here, and are otherwise unused until
`https` is wired.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
