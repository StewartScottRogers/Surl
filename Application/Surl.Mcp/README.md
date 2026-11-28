# Surl.Mcp

`surl-mcp`: an MCP server on standard input and output whose tools let an AI agent start, list
and stop Surl protocol servers and see what their clients sent. Surl is the server-side mate of
curl: for each protocol upstream curl speaks, it is the server that answers.

## Run it

The `Surly.Surl.Mcp` NuGet package is a .NET tool package of type `McpServer`, on nuget.org from
0.1.12. An MCP client runs it with

```
dnx Surly.Surl.Mcp
```

or after `dotnet tool install --global Surly.Surl.Mcp`, as `surl-mcp`.
[GitHub releases](https://github.com/StewartScottRogers/Surl/releases) from `v0.1.10` carry it as a
`.nupkg` file too. To use the file of 0.1.10 or 0.1.11, download it into a folder, give either command
`--add-source <folder>` and name the version (`dnx Surly.Surl.Mcp@0.1.11`, or `--version 0.1.11` on
`dotnet tool install`). A release's `surl-mcp-<rid>` archives hold the same server as a native binary
that needs no .NET runtime.

## Tools

- `start_server` - starts a server from listen URLs and `surl`'s own options.
- `list_servers` - lists the servers this host started.
- `stop_server` - stops one.
- `get_exchanges` - the bytes each client sent and was answered, with the listen URL and scheme of each
  exchange, filtered by scheme and listen URL.
- `get_events` - the events a server kept (exchanges started and ended, logins judged, store changes, faults
  applied, hooks failed, and the kinds `consumer_group_changed` and `cookie_received`), filtered by kind,
  scheme, listen URL, account, `store` and `name` and sequence. An exchange's bytes are never kept as events;
  `get_exchanges` gives them.
- `wait_for_event` - waits up to `timeout_ms` (1 to 60000 milliseconds) for the first event matching the
  same filters, or answers `timed_out`. `store` is a store in snake_case (`content`, `mail`, `bus`, ...) and
  `name` a path, mailbox, topic, queue, key or account, matched exactly or as a segment prefix
  (`/uploads` matches `/uploads/a.txt`, not `/uploads2`), case-sensitive; only store changes match either.
- `seed_mail`, `put_file`, `delete_file`, `publish_message`, `add_account`, `remove_account`, `add_proxy_account`, `remove_proxy_account`, `add_tls_srp_account`, `remove_tls_srp_account`, `set_account_password`, `set_proxy_account_password`, `set_tls_srp_account_password` - change a
  running server's stores as a test would. `delete_file` takes a request `path` and answers `{path}`;
  `no file at <path>` when nothing, a folder or a hidden path is there. `remove_account` takes a `user`
  (an account given at start too; an empty string names the Bearer-token account) and answers `{user}`;
  `server <id> has no account <user>` when it holds no such account, or
  `server <id> has no Bearer-token account` for an empty `user`. Removing the last account adds a `warning`
  member and a second text block, both
  `server <id> has no local account left; it still asks for a login wherever it did`. Neither
  tool needs a client permission (ADR-0161; tests in `DeleteAndRemoveToolsTests`).
  `seed_mail` resolves its `to` as SMTP's `RCPT TO` does (ADR-0050 decision 5, ADR-0102 decision 1.1): without
  a mail domain only the local part counts, matched exactly or else ignoring case, and the domain is ignored,
  so `ALICE@example.com` and `alice@other.org` both deliver to account `alice`. With `--mail-domain`, its
  routing applies and a recipient outside every domain is refused.
- `set_key`, `add_ldap_entries`, `add_dns_zone` - seed the Redis keyspace, the LDAP directory and the DNS
  zones (ADR-0158). `set_key` takes a `key` and a `value` (UTF-8 text, or `key_base64` and `value_base64`;
  together at most 16 MiB), an optional `database` (0 to 15) and `expire_ms` (1 to 31536000000), and answers
  `{key, database, event_name, byte_count}` without echoing the value. `key_base64` takes the place of `key`
  for a key that is not UTF-8, or that holds a control character other than tab, CR and LF; `event_name` is the
  name of the store change (`<database>/<key>`) and `byte_count` the length of the value. `add_ldap_entries`
  takes `ldif` and answers `{entry_count}`; `add_dns_zone` takes `apex` and `zone_text` and answers
  `{apex, byte_count}`. A `changetype:` record, a second zone for an apex, a key past the keyspace's bounds, or
  a server with no Redis, LDAP or DNS server composed is refused with the store's own words.
- `reset_server`, `snapshot_server`, `restore_server` - return a server's stores, the three above included, and
  the AMQP 0-9-1 and MQTT 5 broker state, to their configured state, or save and restore them.
- `list_mailboxes`, `list_messages`, `read_message`, `read_file`, `list_files`, `browse_queue`,
  `read_retained_message`, `read_log` - read back what a client left (ADR-0164), each with a `server_id`.
  They are `list_mailboxes` (`owner`), `list_messages` (`owner`, `mailbox`, `after_uid`, `limit`),
  `read_message` (`owner`, `mailbox`, `uid`), `read_file` (`path`), `list_files` (`path`, `after_name`,
  `limit`), `browse_queue` (`queue`, `after_position`, `limit`), `read_retained_message` (`topic`) and
  `read_log` (`topic`, `from_offset`, `limit`). `read_key` (`key` or `key_base64`, `database`) and `list_keys`
  (`pattern`, `after_key`, `limit`, `database`) read back the Redis keyspace, `read_directory` (`{"ldif"}`, `userPassword` values omitted) the LDAP directory, and `read_zone` (`{"apex","zone"}` for one `apex`, else `{"zones":[...]}`) the DNS zones, the same way (ADR-0164 decision 9): a key
  with its type, size, expiry and, for a string, its value; a listing never holds a value. Bytes come back as
  `text` only when they are well-formed UTF-8 with no control character other than tab, CR and LF, and as
  `base64` otherwise (a log key as `key` or `key_base64`). One item is at most 16 MiB and a list takes `limit` 1 to
  1000 (default 100) with `has_more`. A read changes nothing, publishes no event and trips no fault. An absent
  owner, mailbox, UID, file, queue or log, or a breach of a bound, is `isError: true` with the reason; an
  absent retained message answers `{"retained": false}`, not an error.
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
