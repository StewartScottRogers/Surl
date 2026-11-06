---
id: BL-627
title: Serve the surl --aihelp topics as MCP resources from Surl.Mcp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-623, BL-626]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-627 — Serve the surl --aihelp topics as MCP resources from Surl.Mcp

## Goal

An agent connected to `Surl.Mcp` can list and read the same Markdown `surl --aihelp` prints - the overview, each
topic, and `all` - as MCP resources, so it learns the options, schemes, exit codes and security rules the
`start_server` tool takes without running `surl`. A completeness test keeps the resources in step with the topics.

## Context

- Decision: BL-612's ADR, decision 6 (the resource URIs, names and MIME type; planning `surl://aihelp/<topic>`,
  `text/markdown`).
- Source of the text: `Application/Surl.Cli.UnitLibrary`'s `AiHelpText.Answer(string? topic)` (`null` for the
  overview) and `AiHelpTopics.All` (ADR-0046). `Surl.Mcp.UnitLibrary` references `Surl.Cli` for them; nothing is
  copied or re-worded, so the resources cannot drift from `surl --aihelp`.
- Resource providers: `Surl.McpServing` (BL-623), registered by `Surl.Mcp` with one line.
- The port does the same with its `--ai-help` (`AiHelpMcpResourceProvider`, its ADR-0589); copyable, not evidence.

## Acceptance criteria

- [x] Named tests show: `resources/list` from `RunAsync` lists the overview, one resource per `AiHelpTopics.All`
      entry and `all`; reading each returns exactly the text `AiHelpText.Answer` gives for it; an unknown topic URI
      gets `-32002`.
- [x] A named completeness test fails if a topic is added to `AiHelpTopics.All` without a resource (it enumerates the
      topics rather than pinning a count).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` and `Surl.Mcp` keep
      100% line and branch coverage.

## Notes

- `AiHelpResourceProvider` (Surl.Mcp.UnitLibrary) implements `IMcpResourceProvider`; `Program` adds it with one
  `AddResources` line. URIs, names and MIME type are ADR-0103 decision 6's, so no new ADR was needed.
- Defaults taken: `title` is the topic's `AiHelpTopic.Description` ("Overview and topic list" for the overview,
  "The overview and every topic" for `all`); `description` is one short sentence per resource. The overview is
  read with `Answer(null)`, `all` with `Answer("all")`. An unknown URI never reaches the provider (the registry
  answers `-32002`); the provider's own `null` path is covered by a direct test.
- Tests: `ProgramTests.RunAsync_ResourcesList_ListsTheOverviewEveryAiHelpTopicAndAllInOrder`,
  `RunAsync_ResourcesReadOfEachListedResource_ReturnsExactlyWhatSurlAiHelpPrintsForIt`,
  `RunAsync_ResourcesReadOfAnUnknownTopic_AnswersResourceNotFound`; completeness test
  `AiHelpResourceProviderTests.Resources_EveryAiHelpTopic_HasAResourceNamedForItsSurlAiHelpCommand`.
- Coverage measured with the MSTest collector (cobertura): `Surl.Mcp.UnitLibrary` and `surl-mcp` both 100% line
  and branch. Surl.Mcp.UnitTests: 145 passed.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. surl-mcp serves surl --aihelp's overview, every topic and all as surl://aihelp MCP resources
