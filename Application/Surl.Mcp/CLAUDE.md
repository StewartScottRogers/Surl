# Surl.Mcp

The `surl-mcp` executable (assembly `surl-mcp`, namespace `Surl.Mcp`): Surl's MCP server on standard input and
output, which an MCP client - an AI agent's host - starts as a stdio server
([ADR-0103](../../Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md)
decisions 2, 5 and 7). A naming exception like `Surl.Console`: it is no library, so it has no `.UnitLibrary`
suffix (ADR-0098). It publishes native AOT by default, as `surl` does.

## Only protocol messages reach standard output

Standard output is the MCP channel, and any stray write corrupts it. While serving, only `McpStreamSession`
writes to it, one JSON-RPC message per line. Every log line - each server's start-up warnings, status lines and
exchange log, and the host's own refusals - goes to standard error. Only `--help` and `--version` write to
standard output, and they serve no session.

## What it holds

- `Program` - the composition root. `Main` registers Ctrl+C (SIGINT) and SIGTERM to cancel a token and calls
  `RunAsync(input, output, error, arguments, readEnvironmentVariable, cancellationToken)` with the process's
  standard streams; tests call the overload that also takes a `SurlMcpHostSeams`, with in-memory streams.
  `RunAsync` reads the command line, makes the root (when there is one) its full path and the process's current
  directory - `PathConfinement` takes a relative path from the current directory, so the root must be it - then
  registers each tool with one explicit `McpServerRegistry.AddTool` line (`start_server`, `list_servers`,
  `stop_server`, `get_exchanges`, ADR-0103 decision 3's order, then `get_events` and `wait_for_event`,
  ADR-0124 decision 8's), adds the `surl://aihelp` resources with one
  `AddResources(new AiHelpResourceProvider())` line (decision 6), and serves one session through `McpStreamSession`, with
  `serverInfo` `{"name": "surl-mcp", "title": "Surl", "version": <the assembly's informational version>}` and
  decision 6's `instructions`. When the input ends or the token is cancelled, disposing `RunningSurlServers`
  stops every server it started, and it exits 0. Nothing is found by scanning or reflection.
- `SurlMcpCommandLine` - `--root <dir>` (else `SURL_MCP_ROOT`; empty is unset), `--allow-non-loopback`,
  `--max-servers <n>` (1 to 64), `--log-level <level>` (surl's five words, any case), `--help`, `--version`. A
  later option overrides an earlier one; `--help` or `--version` answers at once. Any other word, a missing
  value or a bad one is refused on standard error as `surl-mcp: option <word>: <why>` with exit 2
  (`FailedInit`); a root that is not a directory is `surl-mcp: (2) the root <dir> is not a directory`.
- `SurlMcpHelpText` - the `--help` text, the version and the `--version` line
  (`surl-mcp <version> (<runtime identifier>)`).
- `SurlMcpHostSeams` - what the host asks of the process: making the `RunningSurlServers`, whether the root
  exists and setting the current directory. `Process` is the real one.

## What it references

`Surl.Mcp.UnitLibrary` only, and through it `Surl.Api`, `Surl.Cli` and `Surl.McpServing`. Tool logic lives in
`Surl.Mcp.UnitLibrary` and session handling in `Surl.McpServing`; keep this project thin.

## How it is tested

`Application/Surl.Mcp.UnitTests` (which tests `Surl.Mcp.UnitLibrary` too): `ProgramTests` runs `RunAsync` over
Surl.McpServing's in-memory `UnitTestChannelInputStream` and `UnitTestLineOutputStream`, starting servers through
`Surl.Api`'s `ApiTestSeams` and fake listener factory, so no socket is opened; `SurlMcpCommandLineTests` covers
the command line.
