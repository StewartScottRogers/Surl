# Surl.Mcp

`surl-mcp`: an MCP server on standard input and output whose tools let an AI agent start, list
and stop Surl protocol servers and see what their clients sent. Surl is the server-side mate of
curl: for each protocol upstream curl speaks, it is the server that answers.

## Run it

The `Surl.Mcp` NuGet package is a .NET tool package of type `McpServer`; an MCP client runs it with

```
dnx Surl.Mcp
```

or after `dotnet tool install --global Surl.Mcp`, as `surl-mcp`. The release's `surl-mcp-<rid>`
archives hold the same server as a native binary that needs no .NET runtime.

## Tools

- `start_server` - starts a server from listen URLs and `surl`'s own options.
- `list_servers` - lists the servers this host started.
- `stop_server` - stops one.
- `get_exchanges` - the bytes each client sent and was answered, with the listen URL and scheme of each
  exchange, filtered by scheme and listen URL.
- `get_events` - the events a server kept (exchanges started and ended, logins judged, store changes, faults
  applied, hooks failed), filtered by kind, scheme, listen URL, account and sequence.
- `wait_for_event` - waits up to `timeout_ms` (1 to 60000 milliseconds) for the first event matching the
  same filters, or answers `timed_out`.
- `seed_mail`, `put_file`, `delete_file`, `publish_message`, `add_account`, `remove_account` - change a
  running server's stores as a test would. `delete_file` takes a request `path` and answers `{path}`;
  `no file at <path>` when nothing, a folder or a hidden path is there. `remove_account` takes a `user`
  (an account given at start too) and answers `{user}`; `server <id> has no account <user>` when it holds
  none. Neither needs a client permission (ADR-0161; tests in `DeleteAndRemoveToolsTests`).
- `reset_server`, `snapshot_server`, `restore_server` - return a server's stores to their configured state,
  or save and restore them.
- `add_fault`, `list_faults`, `remove_fault` - make a listener, account or path misbehave on purpose. There
  is no clear-faults tool (ADR-0161).

The `surl://aihelp` resources carry `surl --aihelp`'s topics.

## Options

| Option | Does |
| --- | --- |
| `--root <dir>` | Confines local paths to `<dir>` (else `SURL_MCP_ROOT`); with neither, no option may name a local path |
| `--allow-non-loopback` | Lets a listen URL name a host other than loopback |
| `--max-servers <n>` | Runs at most `<n>` servers at once, 1 to 64 (default 8) |
| `--log-level <level>` | Logs each server at `none`, `error`, `info`, `verbose` or `trace` (default `info`) |
| `--help`, `--version` | Show the help or the version and exit |

Secure by default: every listen URL must be loopback unless the host was started with
`--allow-non-loopback`, and each server is as strict as `surl` with the same options. Standard
output carries only MCP messages; every log line goes to standard error.
