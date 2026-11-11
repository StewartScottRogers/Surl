---
id: BL-632
title: Drive the built Surl.Mcp over stdio and have pinned upstream curl fetch from the server it starts
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-627, BL-1035]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-632 — Drive the built Surl.Mcp over stdio and have pinned upstream curl fetch from the server it starts

## Goal

An `Integration` test starts the built `Surl.Mcp` as a child process, speaks MCP to it over its standard input and
output, has it start an HTTP server on `127.0.0.1` port 0, runs pinned upstream curl 8.21.0 against the bound URL
the tool returned, reads back through the exchanges tool the request curl sent, stops the server, and closes the
session - proving the whole host end to end with upstream curl as the only judge of the server.

## Context

- Oracle: upstream curl only (ADR-0003). Run it with `PinnedUpstreamCurl.RunAsync`
  (`Conformance/Surl.Conformance.UnitTests/PinnedUpstreamCurl.cs`); inconclusive where the pin is absent.
- The MCP client side is a small hand-written newline-delimited JSON-RPC client in the test project (the port's
  `Curl.Mcp.IntegrationTests` `McpStdioClient` is copyable code: `git -C Z:\repos\Curl show
  origin/master:Curl.Mcp.IntegrationTests/CLAUDE.md`). Run `Surl.Mcp` from its build output with `dotnet exec`
  as that client does, so no publish is needed; add a project reference to `Application/Surl.Mcp` with
  `ReferenceOutputAssembly` as needed so it is built first.
- The tool names, arguments and result shapes are BL-612's ADR decisions 3 and 6, as built by BL-624, BL-626 and
  BL-627. The expected curl result is the one `UpstreamCurlFetchesFromSurlOverHttp11Tests.Get_File_ExitsZeroWithTheFilesBytes`
  pins for `surl`.
- The served file: a temporary data directory holding one file, inside the root the ADR confines tool paths to
  (decision 4), given to the start tool as its data directory and removed when the test ends.
- `[TestCategory("Integration")]` on every test.

## Acceptance criteria

- [ ] A test class `UpstreamCurlFetchesFromSurlMcpServerTests` exists and passes on Windows with the pinned reference
      build present: `initialize` answered; the start tool returns a bound URL with a non-zero port; curl's
      `GET` exits 0 with the served file's bytes; the exchanges tool returns received bytes beginning
      `GET /<file> HTTP/1.1`; the stop tool succeeds; closing standard input ends the process with exit code 0 and
      nothing but JSON-RPC messages was written to its standard output.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, dotnet build cannot start
