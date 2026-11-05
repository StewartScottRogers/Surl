# Surl.Mcp.UnitLibrary

The logic of `surl-mcp`'s tools
([ADR-0103](../../Documentation/Planning/Decisions/ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md)
decisions 2 to 4), namespace `Surl.Mcp`. The executable, `Application/Surl.Mcp` (BL-625), registers each tool
with one `McpServerRegistry.AddTool` line and holds nothing else of the tools.

## What it holds

- `RunningSurlServers` - the servers this host started, keyed by IDs `s1`, `s2`, ... (never reused). It starts
  each through `Surl.Api`'s `SurlServer.StartAsync`, refuses a start past `SurlMcpHostSettings.MaxServers`
  (counting starts in progress), lists them in start order, stops one by ID, and stops every one still running,
  all at once, when disposed.
- `SurlMcpHostSettings` - what the person starting the host decides and an agent cannot change: `Root`,
  `AllowNonLoopback`, `MaxServers` (8 by default) and `LogLevel`.
- The tools, each a static class whose `Create(RunningSurlServers)` returns an `McpTool` with its hand-written
  description and input schema:
  - `StartServerTool` (`start_server`): `listen_urls`, `arguments` (surl's own words), `files` (UTF-8 text) and
    `files_base64`. It checks the words with `StartServerSafetyCheck`, then starts with exchanges recorded
    (100 exchanges, 256 KiB each) and a `StartLogWriter` that copies the server's log to standard error and
    keeps the lines written during the start. Result `{server_id, listen_urls, bound_urls, log_lines}`. A
    `SurlServerStartException` is `isError` with `{exit_code, exit_code_name, lines}` and the text each line
    after `surl: `; an `ArgumentException` from `Surl.Api` or bad `files` is `isError` with its message alone;
    a host rule broken is `isError` `refused: <rule>` with `{refused}`.
  - `ListServersTool` (`list_servers`): `{servers: [{server_id, listen_urls, bound_urls, started_at,
    exchange_count, dropped_exchange_count}]}`; never an error.
  - `StopServerTool` (`stop_server`): `{server_id, exit_code, exit_code_name}`; an unknown ID is `isError`
    `no running server has ID <id>`.
  - `GetExchangesTool` (`get_exchanges`): `server_id`, `after_exchange_id` (only larger IDs) and
    `max_exchanges` (1 to 100, default 20). Result `{server_id, exchanges: [{exchange_id, client, started_at,
    chunks: [{direction, encoding, data}], recorded_bytes, shown_bytes, is_cut, is_truncated}],
    dropped_exchange_count, has_more}`, oldest first; an unknown ID is `isError` as for `stop_server`. There is
    no listen URL per exchange: `Surl.Api` records none (ADR-0099 decision 7).
- `RecordedExchangeWriter` - writes `get_exchanges`'s `exchanges`: a chunk is `text` when it is well-formed
  UTF-8 with no control character but tab, CR and LF, else `base64`. At most 65536 bytes of each exchange and
  1 MiB in all are shown; a cut ends its chunk early and drops the chunks after it (`is_cut`), and in a text
  chunk backs off to the start of a UTF-8 sequence (a chunk cut to nothing is left out). Once the 1 MiB is
  used, later exchanges are left out and `has_more` is true, as it is when `max_exchanges` stops the list.
- `StartServerSafetyCheck` - ADR-0103 decision 4: `-K`/`--config` refused outright; the words parsed with
  `Surl.Cli`'s `CommandLineParser` (accepting every extended scheme, so they parse whenever `Surl.Api`'s own
  parse would; words `surl` refuses are left to `Surl.Api` to refuse with `surl`'s exit code); every listen
  URL on loopback (`localhost`, `127.0.0.0/8`, `::1`, or a Unix domain socket) unless `AllowNonLoopback`; and
  every local path inside the root.
- `CommandLinePathMembers` - which `SurlCommandLine` members hold local paths, with the option a refusal
  names. `CommandLinePathMembersTests` enumerates `SurlCommandLine` by reflection and fails on a member the
  table does not classify: a new path option must be added here.
- `PathConfinement` - with no root every path is refused. With one, a path is taken as the server will open
  it, relative to the process's **current directory**, so the host must set its current directory to the
  root for "relative to the root" to hold (BL-625). The path and the root are resolved component by component,
  following every symbolic link (at most 40) and applying `..` to the resolved directory, then compared
  ordinally.

## What it references

`Surl.Api` (the tools drive `SurlServer`), `Surl.Cli` (`CommandLineParser`, `SurlCommandLine`,
`ExtendedSchemeDefaultPorts`) and `Surl.McpServing` (`McpTool`, `McpToolResult`). It is a composition root
(ADR-0099 decision 11): nothing outside `Application/` references it, and it never references `Surl.Mcp`.

## How it is tested

`Application/Surl.Mcp.UnitTests`, with no network: `McpTestHost` starts servers through `Surl.Api`'s internal
`SurlServerHostSeams`, using `ApiTestSeams` and `FakeListenerFactory` compiled in from `Surl.Api.UnitTests`
(`Surl.Api` makes its internals visible to this test project), and a fixed clock. `PathConfinement` is tested
through fake link tables; only read-only probes of the test's own output directory touch the disk.
