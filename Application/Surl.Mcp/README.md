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
- `set_key`, `add_ldap_entries`, `add_dns_zone` - seed the Redis keyspace, the LDAP directory and the DNS
  zones (ADR-0158). `set_key` takes a `key` and a `value` (UTF-8 text, or `key_base64` and `value_base64`;
  together at most 16 MiB), an optional `database` (0 to 15) and `expire_ms` (1 to 31536000000), and answers
  `{key, byte_count}` without echoing the value. `add_ldap_entries` takes `ldif` and answers `{entry_count}`;
  `add_dns_zone` takes `apex` and `zone_text` and answers `{apex, byte_count}`. A `changetype:` record, a
  second zone for an apex, a key past the keyspace's bounds, or a server with no Redis, LDAP or DNS server
  composed is refused with the store's own words.
- `reset_server`, `snapshot_server`, `restore_server` - return a server's stores, the three above included, and
  the AMQP 0-9-1 and MQTT 5 broker state, to their configured state, or save and restore them.
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
