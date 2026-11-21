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
    a host rule broken is `isError` `refused: <rule>` with `{refused}`. Its description (and `put_file`'s) names
    the 4 MiB `tools/call` message limit (BL-1533); larger seeds go through `put_file`, one file per call.
  - `ListServersTool` (`list_servers`): `{servers: [{server_id, listen_urls, bound_urls, started_at,
    exchange_count, dropped_exchange_count}]}`; never an error.
  - `StopServerTool` (`stop_server`): `{server_id, exit_code, exit_code_name}`; an unknown ID is `isError`
    `no running server has ID <id>`.
  - `GetExchangesTool` (`get_exchanges`): `server_id`, `after_exchange_id` (only larger IDs),
    `max_exchanges` (1 to 100, default 20), and the `scheme` and `listen_url` filters (ADR-0124 decision 8),
    found with `SurlServer.FindExchanges`. Result `{server_id, exchanges: [{exchange_id, listen_url, scheme,
    client, started_at, chunks: [{direction, encoding, data}], recorded_bytes, shown_bytes, is_cut,
    is_truncated}], dropped_exchange_count, has_more}`, oldest first; an unknown ID is `isError` as for
    `stop_server`. Its description names the cut (65536 bytes of each exchange, 1 MiB in all) and the `is_cut`,
    `shown_bytes` and `recorded_bytes` members, building the numbers from `RecordedExchangeWriter`'s constants (BL-1550).
  - `GetEventsTool` (`get_events`): `server_id`, `after_sequence`, `kinds` (derived from the event records, `EventToolFilter.AllKinds`; today `exchange_started`,
    `exchange_ended`, `login_judged`, `store_changed`, `fault_applied`, `hook_failed`, `consumer_group_changed`; `exchange_bytes` is in the enum too but is never kept, so it finds nothing and its description says so, BL-1551), `scheme`, `listen_url`,
    `account`, `store` (a `StoreKind` in snake_case; a schema enum), `name` (a store change's name, exactly or as a
    segment prefix) and `max_events` (1 to 500, default 100), found with `SurlServer.FindEvents`. Result `{events,
    has_more, dropped_event_count}`, oldest first.
  - `WaitForEventTool` (`wait_for_event`): the same filters and `timeout_ms` (1 to 60000, default 10000),
    waited for with `SurlServer.WaitForEventAsync` on the host's clock. Result `{event}`, or `{timed_out: true}`,
    which is no error. `timeout_ms` is range-checked by the tool, not the schema, so a wait out of range is an
    `isError` result rather than a JSON-RPC error. `kinds` may name several kinds and a `SurlEventQuery` names
    one, so an event of a kind not asked for moves the query past it and the wait goes on under one deadline.
  - The store and fault tools (ADR-0124 decision 8, BL-812), each built by `RunningServerTool`: `server_id` is
    required and an unknown one is `isError` as for `stop_server`; any `InvalidOperationException` from
    `Surl.Api` (a seed a store refuses, a store on disk refusing reset or snapshot, a snapshot past 256 MiB, a
    257th fault rule) is `isError` with its message.
    - `SeedMailTool` (`seed_mail`): `to`, and `message` (UTF-8) or `message_base64` -> `{delivered: true}`; on a server started with `--mail-delivered-to`, one `Delivered-To: <to>` line precedes the message (BL-1504).
    - `PutFileTool` (`put_file`): `path`, and `text` or `base64` -> `{path, byte_count}`; a path no request may name (relative, or with `..`) is `isError` saying why, e.g. `path <path> must start with /` (BL-1382, BL-1396).
    - `PublishMessageTool` (`publish_message`): `topic` (with `retain`) or `queue`, and `payload` or
      `payload_base64` -> `{published: true}`; `retain` with `queue` is refused.
    - `DeleteFileTool` (`delete_file`, BL-1175, ADR-0161): `path` -> `{path}`; nothing, a folder or a hidden path there is `no file at <path>`; an unnameable path is the same why-text as for `put_file`.
    - The read-back tools (BL-1201, ADR-0164 decision 6): `ListMailboxesTool` (`list_mailboxes`; a mailbox's `use` such as `\Sent` is a member only when it has one, BL-1514), `ListMessagesTool` (`list_messages`), `ReadMessageTool` (`read_message`), `ReadFileTool` (`read_file`), `ListFilesTool` (`list_files`), `BrowseQueueTool` (`browse_queue`), `ReadRetainedMessageTool` (`read_retained_message`; none is `{retained: false}`) `ReadLogTool` (`read_log`), and for the Redis keyspace (BL-1218, decision 9) `ReadKeyTool` (`read_key`: `key` or `key_base64`, `database`; type, size, expiry and a string's `text` or `base64`), `ReadDirectoryTool` (`read_directory` -> `{ldif}`, BL-1219), `ReadZoneTool` (`read_zone`: `apex` -> `{apex, zone}`, no apex -> `{zones}`, BL-1220), and `ListKeysTool` (`list_keys`: `pattern`, `after_key`, `limit`, `database`; never a value). Bytes are `text` or `base64` by `ReadBackJson`; an absent item or a refusal is `isError` with the API message.
    - `AddAccountTool` (`add_account`): `user`, `password` -> `{user}`. A `:` in the name and a name taken are
      refused with the tool's own text (`user: a user name may not hold ':'`, `user <user> already has an
      account`), so no exception text and never the password is echoed.
    - `SetKeyTool` (`set_key`, BL-1167, ADR-0158 decision 9): `key` or `key_base64`, `value` or `value_base64`,
      `database` (0 to 15), `expire_ms` -> `{key or key_base64, database, event_name, byte_count}` (ADR-0158 amendment 1,
      BL-1405): the key as `read_key` spells it, `event_name` the keyspace event's name (`<database>/<key>`, non-UTF-8
      bytes as `\xHH`, cut at 256 characters); the value is never echoed. On a server with no `redis` listen URL it
      (like `add_dns_zone` and `add_ldap_entries` for their stores) is `isError` with the store's refusal text.
    - `AddLdapEntriesTool` (`add_ldap_entries`): `ldif` -> `{entry_count}` (the `dn:` lines); `AddDnsZoneTool`
      (`add_dns_zone`): `apex`, `zone_text` -> `{apex, byte_count}`. A bound, a duplicate apex or a refusal is `isError`.
    - `RemoveAccountTool` (`remove_account`): `user` -> `{user}`, plus a `warning` member and a second text block `server <id> has no account left and now needs no login` when it was the last account (BL-1391); an account not held is `server <id> has no account <user>`.
    - `AddProxyAccountTool` / `AddTlsSrpAccountTool` (`add_proxy_account`, `add_tls_srp_account`): `user`, `password` -> `{user}`; a server without such accounts is refused (`holds no proxy accounts...`, `was not started with TLS-SRP`). `RemoveProxyAccountTool` / `RemoveTlsSrpAccountTool`: `user` -> `{user}`, same refusals (ADR-0170 decision 4).
    - `SetAccountPasswordTool` / `SetProxyAccountPasswordTool` / `SetTlsSrpAccountPasswordTool` (`set_account_password`, `set_proxy_account_password`, `set_tls_srp_account_password`): `user`, `password` -> `{user}`, never the password; a user with no such account is `user <name> has no account`, and a server without proxy or TLS-SRP accounts is refused as the add tools refuse it (ADR-0132 amendment 1).
    - `ResetServerTool` (`reset_server`) -> `{server_id}`; `SnapshotServerTool` (`snapshot_server`) ->
      `{snapshot_id}`; `RestoreServerTool` (`restore_server`): `snapshot_id` -> `{server_id, snapshot_id}`, and
      an ID not kept is `server <id> has no snapshot <p>; it was never taken of this server, or was dropped as one past its newest 4 snapshots`, or, when another running server keeps that ID, says that server keeps it and a snapshot restores only into the server that took it.
    - `AddFaultTool` (`add_fault`): `fault`, one `--fault` item, read by `Surl.Cli`'s `CommandLineParser` (a bad
      item is `isError` with `surl`'s `option --fault: ...` text) and added as a `SurlFault` -> `{fault_id}`.
    - `ListFaultsTool` (`list_faults`, BL-964): `server_id` -> `{faults: [{fault_id, item, remaining}]}` in plan
      order, read from `SurlServer.Faults.List`; `remaining` is `null` for a rule without `times`.
    - `RemoveFaultTool` (`remove_fault`): `fault_id` -> `{fault_id}`; one the plan does not hold is
      `server <id> has no fault rule <f>`.
  - `SeededContent` reads the content those tools take: exactly one of the text and the base64 property, at most
    16 MiB decoded; no local path is ever read.
  - `ServerSnapshots` - each `RunningSurlServer`'s snapshots, IDs `p1`, `p2` ... per server, never reused, the
    newest 4 kept and the oldest dropped; never returned whole, and forgotten with the server.
  - For every tool taking filters, a `listen_url` that is not an absolute URL is `isError`; `EventToolFilter`
    reads them into the `SurlEventQuery` a .NET test would pass.
- `RecordedEventWriter` - writes an event as `{sequence, at, kind, ...}` through `SurlEvent.WriteMembers` (ADR-0171; a `Kind` member is written `<first word of the kind>_kind`, bytes as hex cut at 256) with enum
  values in snake_case and a `null` member left out, as the control endpoint does (ADR-0124 decision 7).
- `RecordedExchangeWriter` - writes `get_exchanges`'s `exchanges`: a chunk is `text` when it is well-formed
  UTF-8 with no control character but tab, CR and LF, else `base64`. At most 65536 bytes of each exchange and
  1 MiB in all are shown; a cut ends its chunk early and drops the chunks after it (`is_cut`), and in a text
  chunk backs off to the start of a UTF-8 sequence (a chunk cut to nothing is left out). Once the 1 MiB is
  used, later exchanges are left out and `has_more` is true, as it is when `max_exchanges` stops the list.
- `StartServerSafetyCheck` - ADR-0103 decision 4: `-K`/`--config` refused outright; `--log-file`, `--trace` and
  `--trace-ascii` (also `=value` forms) refused with text pointing at standard error, `get_exchanges` and
  `get_events` (BL-1457); the words parsed with
  `Surl.Cli`'s `CommandLineParser` (accepting every extended scheme, so they parse whenever `Surl.Api`'s own
  parse would; words `surl` refuses are left to `Surl.Api` to refuse with `surl`'s exit code); every listen
  URL and every proxy listen URL (`--proxy`) on loopback (`localhost`, `127.0.0.0/8`, `::1`, or a Unix domain socket) unless `AllowNonLoopback`; and
  every local path inside the root.
- `CommandLinePathMembers` - which `SurlCommandLine` members hold local paths, with the option a refusal
  names. `CommandLinePathMembersTests` enumerates `SurlCommandLine` by reflection and fails on a member the
  table does not classify: a new path option must be added here.
- `PathConfinement` - with no root every path is refused. With one, a path is taken as the server will open
  it, relative to the process's **current directory**, so the host must set its current directory to the
  root for "relative to the root" to hold (BL-625). The path and the root are resolved component by component,
  following every symbolic link (at most 40) and applying `..` to the resolved directory, then compared
  ordinally.
- `AiHelpResourceProvider` - `surl --aihelp` as MCP resources (ADR-0103 decision 6, BL-627): `surl://aihelp`
  (the overview), `surl://aihelp/<topic>` for each entry of `AiHelpTopics.All` in its order, and
  `surl://aihelp/all`; `mimeType` `text/markdown`, `name` the `surl --aihelp ...` command that prints it, `title`
  the topic's description. Each read is `AiHelpText.Answer(topic).Output`, so nothing is copied; any other URI
  is answered `-32002`. `AiHelpResourceProviderTests` enumerates `AiHelpTopics.All`, so a new topic without a
  resource fails it.

## What it references

`Surl.Api` (the tools drive `SurlServer`), `Surl.Cli` (`CommandLineParser`, `SurlCommandLine`,
`ExtendedSchemeDefaultPorts`) and `Surl.McpServing` (`McpTool`, `McpToolResult`). It is a composition root
(ADR-0099 decision 11): nothing outside `Application/` references it, and it never references `Surl.Mcp`.

## How it is tested

`Application/Surl.Mcp.UnitTests`, with no network: `McpTestHost` starts servers through `Surl.Api`'s internal
`SurlServerHostSeams`, using `ApiTestSeams` and `FakeListenerFactory` compiled in from `Surl.Api.UnitTests`
(`Surl.Api` makes its internals visible to this test project), and a fixed clock, or `Surl.Api.UnitTests`'
`ManualTimeProvider` where a test moves time on to time a wait out. `PathConfinement` is tested
through fake link tables; only read-only probes of the test's own output directory touch the disk.
