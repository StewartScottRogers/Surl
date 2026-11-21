---
id: BL-1511
title: Write the --aihelp text for folders=<name>=<use>
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1508]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1511 — Write the --aihelp text for folders=<name>=<use>

## Goal

`surl --aihelp` teaches the `folders=<name>=<use>` form so an agent can configure folder uses without reading the ADR.

## Context

ADR-0055 Amendment 1, decision 2. Depends on the parser (BL-1508), so the text describes behaviour that exists. Code: `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` (the mail and `--mailbox` paragraphs) and `AiHelpExamples.cs`. Root `CLAUDE.md` rule: an `AiHelpExamples` entry changes with the behaviour it shows, and `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` runs it. No new option, topic or exit code, so the topic list and fact tables do not grow. Help text only.

## Acceptance criteria

- [ ] The mail topic prose states the `folders=<name>[=<use>]` form, the seven uses (no backslash, case-insensitive), one use per folder and per owner, and that no use is ever implied by a name.
- [ ] An `AiHelpExamples` entry shows `--mailbox "a@x:pw;folders=Sent=sent,Drafts=drafts,Archive"` with the output the test produces.
- [ ] `AiHelpTextTests` and `CommandLineRunnerAiHelpTests` pass, including the examples replay test.
- [ ] `dotnet build` of both projects with `-warnaserror` is clean and coverage stays 100%.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
