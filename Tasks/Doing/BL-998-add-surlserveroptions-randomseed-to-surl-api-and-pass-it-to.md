---
id: BL-998
title: Add SurlServerOptions.RandomSeed to Surl.Api and pass it to the composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-997]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-998 — Add SurlServerOptions.RandomSeed to Surl.Api and pass it to the composition

## Goal

`SurlServerOptions.RandomSeed` (`long?`, null by default) is `--random-seed`'s `Surl.Api` spelling (ADR-0099
decision 4) and reaches the composition, so a .NET test seeds an in-process server as the command line does.

## Context

- Last of the three tasks BL-852 was split into (see BL-996); BL-997 binds the source per composed instance.
- Decision: ADR-0124 decision 6, "The seed's spelling"; ADR-0099 decision 4 (each option has its `Surl.Api`
  property, mapped onto the one configuration model).
- Where: `Application/Surl.Api` (`SurlServerOptions` and its mapping) and its package `README.md` options table if
  it lists every property.
- Stand-in: two servers side by side in one test process, each with its own seed.

## Acceptance criteria

- [ ] A named `Surl.Api` test shows `SurlServerOptions.RandomSeed` reaching the composition, and two servers started
      side by side with different seeds draw different identifier bytes while two with the same seed draw equal ones.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
