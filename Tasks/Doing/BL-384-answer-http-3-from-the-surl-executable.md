---
id: BL-384
title: Answer HTTP/3 from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-379, BL-381, BL-383]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-384 — Answer HTTP/3 from the surl executable

## Goal

The `surl` executable answers HTTP/3 as BL-372's ADR decision 6 decides: it binds the UDP listener for
`https` listen URLs (or for the option the ADR names), serves HTTP/3 through BL-383's server, writes
any `Alt-Svc` field the ADR calls for, and `surl --aihelp` describes it.

## Context

- Decision: BL-372's ADR, decision 6 (default on or off, any option, the port, and `Alt-Svc`).
- `Application/Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` registers each server and its
  listeners. `Surl.Cli.UnitLibrary` holds the option table, `AiHelpProse` and `AiHelpExamples`. Root
  `CLAUDE.md`, "`surl --aihelp`": a new option needs its `OptionArgumentType` and a place on its
  categories' pages, and the behaviour that an `AiHelpProse` paragraph or `AiHelpExamples` entry
  describes changes in the same diff. The tests that enforce this are listed there.
- The status line (`Listening on ...`, read by `SurlOnLoopback`) must say what the UDP listener bound,
  as the ADR decides, so BL-385 can find the port.
- `Alt-Svc` (RFC 7838), if the ADR has responses carry it, is written by `Surl.Protocol.Http`.

## Acceptance criteria

- [ ] `surl https://127.0.0.1:0/` (with `--self-signed`, or with the options the ADR names) binds TCP
      and UDP as the ADR says, and its status lines name both. A `Surl.Console.UnitTests` test pins
      the text.
- [ ] Any new option has its help, `--aihelp` facts and topic text, and `AiHelpFactsTests`,
      `AiHelpTextTests` and `CommandLineRunnerAiHelpTests` pass.
- [ ] If the ADR decides on `Alt-Svc`, an `https` response over HTTP/1.1 and over HTTP/2 carries the
      field the ADR specifies, pinned in a `Surl.Protocol.Http.UnitTests` test.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, the touched projects keep 100%
      line and branch coverage, and `dotnet publish Application/Surl.Console -c Release -r win-x64` succeeds with
      no trim or AOT warning.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Lane 2 could not integrate: fast tests failed twice (Surl.Mcp.UnitTests: Table_EverySurlCommandLineMember_IsClassifiedExactlyOnce; then Surl.Mcp.UnitTests failed) after rebasing onto the other lanes' work. The work is on branch factory/BL-384-lane-2-20261104-205238; start with git cherry-pick --no-commit factory/BL-384-lane-2-20261104-205238 and fix it.
- 2026-11-05: Backlog -> Doing.
