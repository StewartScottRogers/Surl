---
id: BL-1099
title: Lift the refusal of --cert and --self-signed beside TLS-SRP in surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1098]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-1099 — Lift the refusal of --cert and --self-signed beside TLS-SRP in surl

## Goal

`surl --tlsauthtype SRP --tlsuser u --tlspassword p --cert server.pem https://...` (or `--self-signed`)
starts instead of exiting 2, and `--aihelp tls`, `--manual` and the library's CLAUDE.md say so.

## Context

- Split from BL-1051 (timed out). Depends on BL-1098, which serves the certificate once the parser lets it through.
- ADR-0122 decision 2 and Amendment 2: `TlsSrpCombination.FindRefusedBeside` refuses `--cert` and
  `--self-signed` beside TLS-SRP until the signed suites exist. This task removes those two rows.
- Its run wrote this change; it is in the stash named "darkfactory BL-1051 20261112-162230"
  (find N with `git stash list`, matching that message). Take it with:
  - `git diff "stash@{N}^" "stash@{N}" -- Application/Surl.Cli.UnitLibrary/TlsSrpCombination.cs | git apply`
  - `git diff "stash@{N}^" "stash@{N}" -- Application/Surl.Cli.UnitTests/CommandLineParserTlsSrpTests.cs | git apply`
  - `git diff "stash@{N}^" "stash@{N}" -- Application/Surl.Cli.UnitLibrary/AiHelpProse.cs | git apply`
  - `git diff "stash@{N}^" "stash@{N}" -- Application/Surl.Cli.UnitLibrary/ManualText.cs | git apply`
  - `git diff "stash@{N}^" "stash@{N}" -- Application/Surl.Cli.UnitLibrary/CLAUDE.md | git apply`
- The stash's CLAUDE.md line reads awkwardly ("`--tlsv1.3`, (`--cert` and ... BL-1051)"); rewrite it
  to a plain sentence. Check any `--aihelp`/manual text tests that pin the old wording.

## Acceptance criteria

- [ ] `Surl.Cli.UnitTests`: TLS-SRP with `--cert` and with `--self-signed` is served, and the
      refusal tests for them are gone.
- [ ] The `tls` `--aihelp` paragraph and `ManualText`'s TLS-SRP OPTIONS section say `--cert` (RSA key,
      else exit 58) and `--self-signed` add the RSA-signed SRP suites, and no longer list them as refused.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Cli.UnitLibrary`
      keeps 100% line and branch coverage.

## Notes

- Filed by splitting BL-1051 on 2026-11-13.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing.
