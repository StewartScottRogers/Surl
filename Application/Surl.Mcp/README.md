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
- `get_exchanges` - the bytes each client sent and was answered.

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
