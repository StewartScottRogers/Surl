---
id: BL-1389
title: Name a repeated Bearer token, not an empty user, in the --user given-twice refusal
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1389 — Name a repeated Bearer token, not an empty user, in the --user given-twice refusal

## Goal

`surl --user :a --user :b http://127.0.0.1:0/` exits 2 with a message that names the Bearer token, not
`user  is given twice` with an empty name.

## Context

- Surly cookbook bug report 2026-11-18 20:25, finding 1 (commit `a0236e175`).
- `Application/Surl.Cli.UnitLibrary/CommandLineParser.cs:909` - `UserGivenTwice(userName) => $"user {userName} is given twice"`,
  used at `:902` and `:903`. An empty user name is a Bearer token account, so the message reads
  `surl: option --user: user  is given twice`.

## Acceptance criteria

- [x] An empty user name given twice exits 2 (exit code unchanged) with
      `surl: option --user: a Bearer token (empty user name) is given twice`; no token value is shown.
- [x] A non-empty name given twice keeps today's `user <name> is given twice`.
- [x] Named fast tests in `Surl.Cli.UnitTests` pin both messages.
- [x] Any `AiHelpProse` sentence naming this refusal mentions the token form.
- [x] `dotnet build` clean, fast tests green, `Surl.Cli` stays 100/100.

## Notes

Only the --user (account) refusal changed; the authorized-keys user name is never empty (split at the first colon of user:file). Doc-comment: the AiHelpProse sentence states the message in prose because a test pins every quoted `surl:` line.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Doing -> Done. Bearer token repeat now named in the --user given-twice refusal
