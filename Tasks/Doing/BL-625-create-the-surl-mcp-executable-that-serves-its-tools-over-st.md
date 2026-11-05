---
id: BL-625
title: Create the Surl.Mcp executable that serves its tools over standard input and output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-621, BL-624]
touches: [Application/Surl.Mcp, Application/Surl.Mcp.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-625 — Create the Surl.Mcp executable that serves its tools over standard input and output

## Goal

`Application/Surl.Mcp` is an executable that an MCP client (an AI agent's host) starts as a stdio server: it
registers each `Surl.Mcp.UnitLibrary` tool with one explicit line, serves one MCP session on its own standard input
and output through `Surl.McpServing`, logs only to standard error, and stops every Surl server it started when its
input closes or it receives Ctrl+C or SIGTERM. It publishes native AOT.

## Context

- Decision: BL-612's ADR, decisions 2, 5 and 7 (project and assembly name, planning `surl-mcp`; lifetime; native AOT
  publishing). BL-610's ADR makes `Surl.Mcp` a naming exception like `Surl.Console`.
- Composition root rules, as `Application/Surl.Console/CLAUDE.md` states them for `surl`: every object constructed
  explicitly, no assembly scanning or reflection-based DI; `Program.Main` registers Ctrl+C and SIGTERM to cancel a
  token and calls an internal `RunAsync(input, output, error, cancellationToken)` that tests call with in-memory
  streams. Keep the project thin: tool logic is in `Surl.Mcp.UnitLibrary`, session handling in `Surl.McpServing`.
- The csproj mirrors `Application/Surl.Console/Surl.Console.csproj`: `OutputType` `Exe`, the assembly name the ADR
  decides, `PublishAot` on by default, `InvariantGlobalization`. Port model, copyable but never evidence:
  `git -C Z:\repos\Curl show origin/master:Curl.Mcp/CLAUDE.md` and `Curl.Mcp/Curl.Mcp.csproj`.
- Standard output is the protocol channel: any stray write corrupts it (MCP "Transports > stdio"). The `Surl.Api`
  servers' start-up log writer goes to standard error.

## Acceptance criteria

- [ ] `Application/Surl.Mcp` exists in `Surl.slnx`'s `/Application/` folder, with a `CLAUDE.md` saying what it is, what
      it references and that only protocol messages reach standard output.
- [ ] Named tests in `Surl.Mcp.UnitTests` call `RunAsync` over in-memory streams and show: `initialize` answered with
      the ADR's server name and the `tools` capability; `tools/list` listing exactly the library's tools in the
      ADR's order; a `start_server` call on `http://127.0.0.1:0` (with the fake listener seam) answered; closing the
      input stopping that server and returning exit code 0; standard output holding only one JSON-RPC message per
      line while a start writes a warning to standard error.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp` has 100% line and branch
      coverage.
- [ ] `dotnet publish Application/Surl.Mcp -c Release -r win-x64` produces a native binary with no AOT or trim
      warning (run once on Windows and the result recorded in Notes; vswhere on PATH as root `CLAUDE.md` says).

## Notes

- From BL-624 (2026-11-05): `PathConfinement` checks a relative path as the server will open it, relative to
  the process's current directory. So when `--root` or `SURL_MCP_ROOT` is given, `surl-mcp` must set
  `Environment.CurrentDirectory` to the root before it serves, or relative paths are taken from elsewhere (and
  refused). See `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
