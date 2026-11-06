---
id: BL-628
title: Point surl --aihelp's overview at Surl.Api and Surl.Mcp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-617, BL-627]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-628 — Point surl --aihelp's overview at Surl.Api and Surl.Mcp

## Goal

An agent that reads `surl --aihelp` learns, in the overview, that the same servers can be started in-process from
.NET through `Surl.Api` and by an MCP client through `Surl.Mcp`, with one sentence each on what they are for and
where their own help lives - `Surl.Api`'s package README and XML documentation, and `Surl.Mcp`'s tool descriptions
and its `--aihelp` resources.

## Context

- Decisions: BL-611's ADR decision 9 and BL-612's ADR decision 6 say whether and how `surl --aihelp` points at the
  hosts. Planning assumption: one short paragraph in the overview, no new topic (a topic would change the pinned
  topic list of ADR-0046). If the ADRs decided otherwise, follow them and record it in Notes.
- The overview text is `AiHelpProse.OverviewAbout` (or the section the ADR names) in
  `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`; `AiHelpText.Answer(null)` builds it. Its pinned tests are in
  `Application/Surl.Cli.UnitTests` (`AiHelpTextTests`).
- Every statement must be true of the code when this task runs (root `CLAUDE.md`, "Say what it does"): name the
  real project, type and tool names `Surl.Api` (BL-615 to BL-617) and `Surl.Mcp` (BL-624 to BL-627) have.
- `surl --aihelp` is served as an MCP resource by `Surl.Mcp` (BL-627), so the text must also read correctly there.

## Acceptance criteria

- [ ] `surl --aihelp` (`AiHelpText.Answer(null)`) contains the paragraph, naming `Surl.Api`'s starting type and
      `Surl.Mcp`'s executable name and tools, and a named test in `AiHelpTextTests` pins it.
- [ ] Every `AiHelpTextTests`, `AiHelpFactsTests` and `CommandLineRunnerAiHelpTests` test passes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Cli` keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
