---
id: BL-058
title: Carry the per-exchange limits as ExchangeLimits in SurlCommandLine
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-046, BL-014]
touches: [Surl.Cli.UnitLibrary, Surl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-058 — Carry the per-exchange limits as ExchangeLimits in SurlCommandLine

## Goal

`SurlCommandLine` carries the per-exchange limits as one `ExchangeLimits` member, as
ADR-0007 section 3 says, in place of the five separate members BL-014 gave it.

## Context

- ADR-0007 section 3: the parsed command line carries `ExchangeLimits` (ADR-0006
  section 6) for the per-exchange limits.
- BL-014 landed before `ExchangeLimits` existed (BL-046 adds it to
  `Surl.Protocol.Abstractions`), so `SurlCommandLine` holds `HeadTimeout`,
  `MaxRequestHeadBytes`, `MaxLineBytes`, `MaxMessageBytes` and `MaxUploadBytes`
  directly, named as `ExchangeLimits`' members. See BL-014's Notes.
- The option rows live in `Surl.Cli.UnitLibrary/CommandLineOptions.cs`; each setter
  becomes `c with { Limits = c.Limits with { … } }`.

## Acceptance criteria

- [ ] `SurlCommandLine` has a `Limits` member of type `ExchangeLimits`, defaulting to
      `ExchangeLimits.Default`, and no longer has the five separate limit members.
- [ ] `--head-timeout`, `--max-request-head`, `--max-line`, `--max-message` and
      `--max-filesize` set the matching `ExchangeLimits` member; the existing
      `CommandLineParserTests` for those options pass against the new member.
- [ ] A test asserts `new SurlCommandLine().Limits` equals `ExchangeLimits.Default`.
- [ ] `dotnet build` is clean, the fast tests are green, and `Measure-CodeQuality.ps1`
      reports no failing member in `Surl.Cli.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
