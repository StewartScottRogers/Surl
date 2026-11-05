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
completed: 2026-11-05
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

- [x] `Application/Surl.Mcp` exists in `Surl.slnx`'s `/Application/` folder, with a `CLAUDE.md` saying what it is, what
      it references and that only protocol messages reach standard output.
- [x] Named tests in `Surl.Mcp.UnitTests` call `RunAsync` over in-memory streams and show: `initialize` answered with
      the ADR's server name and the `tools` capability; `tools/list` listing exactly the library's tools in the
      ADR's order; a `start_server` call on `http://127.0.0.1:0` (with the fake listener seam) answered; closing the
      input stopping that server and returning exit code 0; standard output holding only one JSON-RPC message per
      line while a start writes a warning to standard error.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp` has 100% line and branch
      coverage.
- [x] `dotnet publish Application/Surl.Mcp -c Release -r win-x64` produces a native binary with no AOT or trim
      warning (run once on Windows and the result recorded in Notes; vswhere on PATH as root `CLAUDE.md` says).

## Notes

- From BL-624 (2026-11-05): `PathConfinement` checks a relative path as the server will open it, relative to
  the process's current directory. So when `--root` or `SURL_MCP_ROOT` is given, `surl-mcp` must set
  `Environment.CurrentDirectory` to the root before it serves, or relative paths are taken from elsewhere (and
  refused). See `Application/Surl.Mcp.UnitLibrary/CLAUDE.md`.
- Done (2026-11-05): `RunAsync` makes the root its full path (`Path.GetFullPath`), checks it is a directory
  (else `surl-mcp: (2) the root <dir> is not a directory`, exit 2), sets it as the current directory through
  `SurlMcpHostSeams.SetCurrentDirectory`, and passes the full path as `SurlMcpHostSettings.Root`.
- Plan, built as ADR-0103 decisions 2, 5, 6 and 7 say: `Program` (signals, two `RunAsync` overloads - the
  production one with `SurlMcpHostSeams.Process`, the test one taking seams), `SurlMcpCommandLine` and its
  result, `SurlMcpHelpText`, `SurlMcpHostSeams`. Tools registered: `start_server`, `list_servers`, `stop_server`
  (the library has no `get_exchanges` yet; BL-626 adds its one line). Tests live in the existing
  `Surl.Mcp.UnitTests`, which ADR-0103 decision 2 says tests both projects; it links Surl.McpServing.UnitTests'
  `UnitTestChannelInputStream` and `UnitTestLineOutputStream` rather than copying them.
- Defaults taken (choices with an obvious default, no ADR needed):
  - A later option overrides an earlier one; `--help` or `--version` answers at once as the first read, ignoring
    later words, so `surl-mcp --help --bogus` shows the help.
  - Refusals read `surl-mcp: option <word>: <why>` (as `surl`'s `surl: option --bogus: is unknown`): `is unknown`,
    `needs an argument`, `expected a number from 1 to 64, got '<v>'` (digits only), `expected none, error, info,
    verbose or trace, got '<v>'` (any case, as `surl --log-level`); an empty `--root` is refused, an empty
    `SURL_MCP_ROOT` counts as unset.
  - `--version` writes `surl-mcp <version> (<runtime identifier>)`, the version being the assembly's informational
    version without build metadata, the same string `serverInfo.version` carries (ADR-0103 decision 6 says
    "surl's informational version"; both assemblies get the one version from the build).
  - The help does not list the tools, so it cannot drift as BL-626 and BL-627 add more.
  - `instructions` follows decision 6 and already names the `surl://aihelp` resources, which BL-627 serves, as
    `start_server`'s own description already does.
- Coverage (2026-11-05, `dotnet test --collect "Code Coverage;Format=cobertura"`): the `surl-mcp` assembly is at
  100% line and 100% branch.
- Native AOT (2026-11-05, Windows, vswhere on PATH): `dotnet publish Application/Surl.Mcp -c Release -r win-x64`
  wrote `surl-mcp.exe` (13,091,328 bytes) with no warning of any kind; piping one `initialize` request into it
  answered with `serverInfo` `{"name":"surl-mcp","title":"Surl","version":"1.0.0"}` and the `tools` capability,
  exit 0 when the input ended; `surl-mcp --version` printed `surl-mcp 1.0.0 (win-x64)`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. surl-mcp serves start_server, list_servers and stop_server over stdio, stops its servers on EOF or a signal, and publishes native AOT
