---
id: BL-227
title: Record in ADR-0051 that --throwaway-hostkey is in the security category too
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-227 — Record in ADR-0051 that --throwaway-hostkey is in the security category too

## Goal

ADR-0051 decision 5's option table says what the code does: `--throwaway-hostkey` is listed in
the `security` and `testing` categories until BL-171, not `testing` alone.

## Context

- BL-158 parsed the ADR-0051 decision 5 options. Its table gives `--throwaway-hostkey` the
  category `testing` only, but `AiHelpFactsTests.LoosensSecurityForTestsOnly_IsExactlyTheFiveTestingOptions`
  holds every `testing` option to be in `security` as well (as `--self-signed` is, ADR-0034), so
  BL-158 listed it in both. BL-158 could not edit the ADR: BL-173 held
  `Documentation/Planning/Decisions` at the time.
- Source of truth: `Surl.Cli.UnitLibrary/CommandLineOptions.cs`, the `throwaway-hostkey` row.

## Acceptance criteria

- [ ] ADR-0051 decision 5's table row for `--throwaway-hostkey` names `security`, `testing`,
      with one sentence saying why (every loosening option is also a security option).

## Notes

## Log

- 2026-09-29: Created.
