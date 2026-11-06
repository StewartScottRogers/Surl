---
id: BL-624
title: Create Surl.Mcp.UnitLibrary with tools that start, list and stop Surl servers through Surl.Api
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-612, BL-616, BL-620]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Surl.slnx, Application/Surl.Api]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-624 — Create Surl.Mcp.UnitLibrary with tools that start, list and stop Surl servers through Surl.Api

## Goal

`Application/Surl.Mcp.UnitLibrary` holds the logic of the MCP tools that start a Surl server (listen URLs and the
options `Surl.Api` takes), list the running ones with their bound URLs, and stop one - each a `Surl.McpServing` tool
with a hand-written description and input schema, applying the safety rules BL-612's ADR decides, and answering every
start failure as an `isError` result that names its `SurlExitCode` and `surl`'s text.

## Context

- Decision: BL-612's ADR, decisions 2, 3 and 4 (project split, tool names, descriptions and schemas, result shapes,
  and safety: which listen hosts, how many servers, which local paths and the root they are confined to). Use its
  names; the planning names are `start_server`, `list_servers` and `stop_server`.
- The tools drive `Surl.Api` (BL-615, BL-616): `SurlServer.StartAsync` with `SurlServerOptions`, `BoundUrls`,
  `StopAsync` (or the names BL-611's ADR decided). The library keeps the running servers, keyed by an ID the tool
  returns, and stops them all when disposed.
- Tool model and registry: `Surl.McpServing` (BL-620). Tool logic lives here; the executable (`Application/Surl.Mcp`,
  the next task) only registers each tool with one line.
- Tests use `Surl.Api`'s fake-listener seam (no network) and an injected `TimeProvider`. `Application/Surl.Mcp.UnitTests`
  tests this library now and the executable later (BL-612's ADR decision 2). Port model, copyable but never
  evidence: `git -C Z:\repos\Curl show origin/master:Curl.Mcp.UnitLibrary/CLAUDE.md`.

## Acceptance criteria

- [x] `Application/Surl.Mcp.UnitLibrary` and `Application/Surl.Mcp.UnitTests` exist, are in `Surl.slnx`'s
      `/Application/` folder in ordinal order, and the library has a `CLAUDE.md` saying what it holds and what it
      references.
- [x] Named tests show: a start on `http://127.0.0.1:0` returning the server's ID and bound URL; a list naming each
      running server with its listen and bound URLs; a stop removing it from the list; stopping an unknown ID an
      `isError` result; an `https` start with no certificate an `isError` result naming exit code 58 and `surl`'s
      text; each safety rule the ADR decides refused as it says (a non-loopback host, one server past the limit, a
      path outside the root); disposing the library's server set stopping every server.
- [x] A named test checks each tool's input schema is a JSON object schema and its description is not empty.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Plan (ADR-0103 decisions 2-4): `RunningSurlServers` (the ID-keyed set, the server limit, stop-all on
  dispose), `StartServerTool`/`ListServersTool`/`StopServerTool` (each `Create(RunningSurlServers)` -> `McpTool`),
  `StartServerSafetyCheck`, `CommandLinePathMembers` (+ reflection completeness test), `PathConfinement`,
  `StartLogWriter`, `ToolResults`. 76 tests; library at 100% line, 100% branch, worst CRAP 10.
- Touches widened: `Application/Surl.Api` gains `InternalsVisibleTo Surl.Mcp.UnitTests`, so the tests start
  servers through `SurlServerHostSeams` and `ApiTestSeams`/`FakeListenerFactory` (compiled in by link from
  `Surl.Api.UnitTests`) with no socket, as the Context asks. No task in Doing touched `Surl.Api`.
- Decision (default taken): a relative path is checked as the server will open it, from the process's current
  directory, rather than rewriting surl's words to absolute paths (which would need the parser's internal option
  table). ADR-0103's "relative to the root" holds once the host sets its current directory to the root; noted on
  BL-625. Any other current directory refuses relative paths, so it fails safe.
- Decision: the safety parse accepts every extended scheme (`ExtendedSchemeDefaultPorts.Schemes`), a superset
  of what `Surl.Api` parses, so words that parse for `Surl.Api` always parse for the check; words that do not
  parse are left for `Surl.Api` to refuse with `surl`'s exit code. `-K` is refused as any word starting `-K`,
  and `--config` / `--config=...` (the parser has no `-K` yet).
- Decision: bad `files`/`files_base64` entries (non-string, bad base64, a path given twice) are `isError` with
  a message naming the path, shaped like `Surl.Api`'s `ArgumentException` failures (text only, no exit code).
- Path comparison is ordinal on every platform: a path spelled in another case from the root is refused on
  Windows rather than guessed at.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Mcp.UnitLibrary's start_server, list_servers and stop_server tools start, list and stop Surl servers through Surl.Api under ADR-0103's loopback, server-limit and root rules
