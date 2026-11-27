# Surl as a stand-in for tests

A **stand-in** is a real Surl server that someone else's integration test runs against in place of
the real mail server, bucket, broker or file site - often several protocols at once (send over SMTP
and read over IMAP; upload over SFTP and check over HTTP; publish over MQTT and consume over NATS or
STOMP). The decision is [ADR-0124](../Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) and
root `CLAUDE.md`'s section "Surl is a stand-in for tests". Every example below is one a recipe test in
`Conformance/Surl.Conformance.UnitTests` runs, or is copied from the `Surl.Api` README, which its own
tests cover. Terms are in the [glossary](Glossary.md#stand-in-for-tests).

## The principles

- **Embeddable.** A test starts Surl inside itself through `Surl.Api` on port 0, in memory, with many
  instances side by side. No static mutable state.
- **Composable.** Any mix of protocols runs in one instance, and servers that do the same job share one
  store, so a cross-protocol scenario needs no glue.
- **Observable.** Each instance publishes every exchange and store change on one event stream; the
  exchange record, queries, waits, `Surl.Mcp`, the control endpoint and hooks all subscribe to it.
- **Controllable.** Stores are seeded before a test, reset between tests, snapshotted and restored.
- **Fault-injectable.** A server misbehaves on purpose - delay, drop, refuse a login, answer a chosen
  error, run out of space - per listener, account or path.
- **Deterministic.** Time comes from the injected `TimeProvider`; the non-secret identifiers are seedable.
- **Open for extension.** New protocols plug in by explicit registration, never a central switch.
- **Configuration as data.** One model serves the command line, `-K` files, `Surl.Api`, `Surl.Mcp` and the
  control endpoint, and it only grows.

All of it sits beside secure by default: anything that widens what a peer or a test may do is off until
asked for, and warned.

## Embedding with `Surl.Api`

```csharp
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello"u8.ToArray();
await using var server = await SurlServer.StartAsync(options);
// server.BoundUrls lists every listen URL, in order, with its bound port.
```

Port 0 takes a free port, so tests never collide. Several servers run at once; each records only its own
exchanges and is changed only by its own faults. Recipe: `StandInParallelInstancesRecipeTests`
(`TwoServers_EachServesItsOwnFileAndRecordsOnlyItsOwnExchanges`,
`FaultAddedToOneServer_DoesNotAffectTheOther`). The full property list is in
[`Application/Surl.Api/README.md`](../../Application/Surl.Api/README.md); the overview is in the README's
[Using Surl from a test suite or an AI agent](../../README.md#using-surl-from-a-test-suite-or-an-ai-agent).

## Composing protocols over the shared stores

One instance, one mail store, one content store, one message bus and one account store (ADR-0100), plus one
Redis keyspace, one LDAP directory and one set of DNS zones (ADR-0158). Mail
sent over SMTP is read over IMAP or POP3; a file uploaded over FTP or SFTP is fetched over HTTP, and a file uploaded over FTP is fetched over SMB too
(`UpstreamCurlSharesFilesFromFtpToSmbTests`); a message
published over MQTT reaches NATS and STOMP. The stores are configured by the layer-2 options (`--mail-domain`,
`--mailbox`, `--share`, `--bucket`, `--topic`, `--queue`, ...), which `SurlServerOptions` carries as
`MailDomains`, `Mailboxes`, `Shares`, `Buckets`, `Topics`, `Queues` and the like; see the README's
[Configuring a real server](../../README.md#configuring-a-real-server).

## Observing

Set `RecordExchanges` and read `server.Exchanges`: each `SurlExchangeRecord` has the listen URL that
accepted it, its scheme, the client's endpoint and the bytes both ways. Ask for what a test cares about
with a `SurlEventQuery` through `FindExchanges`, `FindEvents`, `WaitForExchangeAsync` and
`WaitForEventAsync`; a wait checks what is kept first and its timeout runs on `TimeProvider`, so a test
never sleeps or polls (ADR-0143):

```csharp
var delivered = await server.WaitForEventAsync(
    new SurlEventQuery { Scheme = "smtp", Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Account = "bob" },
    TimeSpan.FromSeconds(5));
```

`SurlEventQuery.Name` finds one store change by what it touched: the path, mailbox, topic, queue, key or
account, matched exactly or as a segment prefix (`/uploads` matches `/uploads` and `/uploads/report.csv`, not
`/uploads2`), case-sensitive, and only a `StoreChanged` event matches (ADR-0164 decision 8):

```csharp
var uploaded = await server.WaitForEventAsync(
    new SurlEventQuery { Store = StoreKind.Content, Name = "/uploads/report.csv" }, TimeSpan.FromSeconds(5));
```

## Controlling: seed, reset, snapshot, restore

```csharp
await server.Mail.DeliverAsync("alice@example.com", "Subject: hi\r\n\r\nhello\r\n"u8.ToArray());
await server.Files.PutAsync("/reports/a.txt", "contents"u8.ToArray());
await server.Messages.PublishAsync("sensors/1", "21.5"u8.ToArray(), retain: true);
server.Accounts.Add("bob", "builder");
var clean = await server.SnapshotAsync();
await server.RestoreAsync(clean);       // back to the snapshot
await server.ResetAsync();              // or back to how the server started
```

The Redis keyspace, the LDAP directory and the DNS zones are seeded the same way (ADR-0158):

```csharp
await server.Keys.SetAsync("seeded", "yes", TimeSpan.FromHours(1), database: 0);          // a string key, as SET would
await server.Directory.AddEntriesAsync("dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n");   // LDIF content records
await server.Zones.AddZoneAsync("example.com", zoneFileText);                              // one zone, from zone-file text
```

A seed goes through the path a client would write by, so names, limits and events apply, and a seed never
trips a fault rule; one a client would be refused throws `SurlSeedRefusedException`. A snapshot is an
in-memory copy that only its own server restores; with `DataDirectory` set, reset and snapshot throw
`InvalidOperationException` so a fixture never empties a real directory. Recipes:
`StandInMailRoundTripRecipeTests.SeedMessage_ThenReadOverPop3_ReturnsTheSeededMessage` and
`ResetAsync_AfterSeeding_LeavesTheMailboxEmptyForTheNextTest`; for the directory,
`StandInLdapDirectoryRecipeTests` (`SeedEntry_ThenSearchOverLdap_PrintsTheSeededDnAndAttribute`,
`ResetAsync_AfterSeeding_TheSameSearchNoLongerFindsTheEntry`). A key is at most 16 MiB with its value, in
database 0 to 15 and living 1 millisecond to 365 days; an LDIF seed and a zone are at most 16 MiB each; the
directory refuses a `changetype:` record and a value read from a file or URL, and the zones refuse a second
zone for an apex you did not reset. `ResetAsync` and `RestoreAsync` cover these three stores as well as the
first four, and also the broker state the message bus carries: the AMQP 0-9-1 broker's declared exchanges and
bindings and the MQTT 5 broker's sessions kept with no connection (ADR-0159). Unit tests:
`SurlServerTests.Lookups`.

## Reading back what a client left

A test checks the mail store, the content store and the message bus without opening its own IMAP, HTTP or AMQP
client (ADR-0164):

```csharp
var mail = await server.Mail.ListMessagesAsync("bob", "INBOX");                 // UID, size, flags, internal date
var message = await server.Mail.ReadMessageAsync("bob", "INBOX", mail.Messages[0].Uid);   // Summary and Bytes
var file = await server.Files.ReadAsync("/reports/a.csv");                      // Bytes, ByteCount, ModifiedAt, ContentType
var folder = await server.Files.ListAsync("/reports");                          // Entries (Name, IsFolder, ByteCount)
var queued = await server.Messages.BrowseQueueAsync("orders");                  // ready messages, never consumed
var retained = await server.Messages.ReadRetainedAsync("sensors/1");            // null when none
var log = await server.Messages.ReadLogAsync("events", fromOffset: 0);          // records and NextOffset
var key = await server.Keys.GetAsync("session:1");                              // Info (type, size, expiry) and Text
var keys = await server.Keys.ListAsync("session:*");                            // names, types and sizes, no values
var ldif = await server.Directory.ExportLdifAsync();                                // the LDAP directory as LDIF, no userPassword
var zone = await server.Zones.ReadAsync("example.com");                                 // one DNS zone as master-file text (Zones.ListAsync lists the apexes)
```

`ListMailboxesAsync(owner)` lists an owner's mailboxes. A read changes nothing, publishes no event, never
trips a fault rule and works on a `DataDirectory` server. One item is at most 16 MiB and a list takes `limit`
1 to 1000 (default 100), answering `HasMore`; a breach throws `SurlReadRefusedException`, an absent item
`SurlNotFoundException`. The `.surl` state folder is never served. The same reads are `Surl.Mcp` tools and
control-endpoint routes, below.

## Faults

A `SurlFault` is one `--fault` item: `Delay`, `DropConnection`, `RefuseLogin`, `ProtocolError`, `OutOfSpace`,
narrowed by `Listener`, `Scheme`, `Account`, `Path`, `Command`, `Stage`, `AfterBytes` and `Times`. A `drop` with `after-bytes` takes `close=reset` (the default, an abort) or
`close=graceful` (a clean close after the N-th byte, so a client sees a short body, not a reset), as in
`--fault "drop;after-bytes=4000000;close=graceful"`; `close` needs `after-bytes` above 0. Over HTTP/3,
`graceful` closes the QUIC connection with `CONNECTION_CLOSE` `NO_ERROR` (upstream curl 8.21.0 exits 56 at
once) and `reset` only stops answering (curl waits for its own timeout; ADR-0188 amendment 5). Give them
at start in `Faults`, or at run time with `server.Faults.Add`, `Remove`, `Clear` and `List`. `surl --aihelp`
documents the `--fault` grammar; `Surl.Mcp` has `add_fault`, `list_faults` and `remove_fault`, and the
control endpoint has `POST /v1/faults`. Recipe: `StandInFaultRecipeTests`
(`RefuseLogin_ForAlice_MakesCurlFailWith401AndExit22`,
`ProtocolError_OnSmtpRcptTo_MakesCurlFailWithTheChosenReply`,
`DropConnection_Once_FailsTheFirstCurlAndLetsTheRetrySucceed`). Upstream curl 8.21.0 exits 56, 55 or 7 on a
connection `DropConnection` resets before any byte, by which side sees the reset first (7 when it arrives before
curl's own connect check finishes), so a test of a dropped connection accepts all three.

## Hooks and their bounds

`server.OnEvent(query, callback)` runs test code for each matching event; `server.Faults.AddOverride(scope,
decide)` computes a fault per request. Hooks are `Surl.Api` only. A callback never blocks a server (it has
its own bounded subscription and loses its oldest events if slow); an override is bounded by
`HookTimeLimit` (1 second by default); a hook that throws or times out is caught and published as a
`HookFailedEvent`; and a hook can choose only what a fault rule can, so it cannot loosen a secure default.
Details in the `Surl.Api` README, "Hooks".

## Your own protocol server

`options.ProtocolServers.Add(new SurlProtocolServer("echo", services => new EchoServer(services.ContentStore)))`
registers a server for a new scheme, served beside the built-in ones through the same listeners, limits,
event stream and fault plan. Add only: a scheme Surl already serves is an `ArgumentException`. See the
`Surl.Api` README, "Your own protocol server".

## Determinism

`SurlServerOptions.TimeProvider` is the one clock every exchange and every wait runs on
(`TimeProvider.System` by default), so a test with a fake clock controls timestamps and timeouts.
`RandomSeed` (`--random-seed`, 0 to `long.MaxValue`, also `random-seed = <n>` in a `-K` file) makes the
non-secret identifiers repeat. Keys, nonces, challenges, salts, session and connection tokens and
cookies are never seeded (ADR-0124 decision 6); without a seed nothing is.

## The control endpoint, for suites not written in .NET

Started from `surl` or from `Surl.Api` (`SurlServerOptions.ControlListenUrl`, `ControlTokenFile`;
`SurlServer.ControlUrl` is the bound URL), as in
`options.ControlListenUrl = "http://127.0.0.1:0"; options.ControlTokenFile = "control.token";` on a `SurlServerOptions`
(`SurlServerTests.Control`; the `Surl.Api` README, "Control endpoint from a .NET harness").
`surl --control http://127.0.0.1:0 --control-token-file control.token <listen URLs>` serves HTTP and JSON
under `/v1/` on loopback, guarded by the bearer token (the file's first line, 32 to 1024 visible ASCII
bytes); `--control` without a token file is refused, and a request without the token gets `401`. Routes:
`GET /v1/events`, `GET /v1/events/wait`, `GET /v1/exchanges`, `GET` and `POST /v1/faults`,
`DELETE /v1/faults/<id>`, `POST /v1/mail?to=<mailbox>`, `PUT` and `DELETE /v1/files/<path>`,
`POST /v1/messages`, `PUT /v1/keys/<database>/<key>` (`expire_ms` query), `POST /v1/directory` (LDIF body),
`PUT /v1/zones/<apex>` (zone-file body), `POST /v1/accounts`, `PUT` and `DELETE /v1/accounts/<name>`,
`PUT` and `DELETE /v1/bearer-token`, `POST /v1/proxy-accounts`, `PUT` and `DELETE /v1/proxy-accounts/<name>`,
`POST /v1/tls-srp-accounts`, `PUT` and `DELETE /v1/tls-srp-accounts/<name>`, `POST /v1/reset`,
`POST /v1/snapshots` and `POST /v1/snapshots/<id>/restore`. An account `POST` takes JSON
`{"user", "password"}` and an account or bearer-token `PUT` takes `{"password"}`. `/v1/bearer-token`
addresses the account with the empty user name, whose password is a Bearer token (`"user": ""` on
`POST /v1/accounts` adds it), not the token that guards the control endpoint. The read-back routes are all `GET`:
`/v1/mail/<owner>`, `/v1/mail/<owner>/<mailbox>` (`after_uid`, `limit`), `/v1/mail/<owner>/<mailbox>/<uid>`,
`/v1/files/<path>` (the bytes), `/v1/files/<path>/` (a listing; `after_name`, `limit`),
`/v1/queues/<name>/messages` (`after_position`, `limit`), `/v1/topics/<name>/retained` and
`/v1/topics/<name>/log` (`from_offset`, `limit`), `/v1/keys/<key>` (`database`) and `/v1/keys` (`pattern`, `after_key`,
`limit`, `database`), `/v1/directory` (LDIF, no `userPassword`), `/v1/zones` (the apexes) and
`/v1/zones/<apex>` (one zone as master-file text). `GET /v1/events` and `GET /v1/events/wait` also take `store`
and `name`. With curl, as the recipe runs it:

```
curl -sS --fail-with-body -X POST -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: message/rfc822" --data-binary @seed.eml \
     "http://127.0.0.1:<control port>/v1/mail?to=ann@example.test"
curl -sS --fail-with-body -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: text/plain" \
     -d "refuse-login;account=ann@example.test" "http://127.0.0.1:<control port>/v1/faults"
curl -sS --fail-with-body -H "Authorization: Bearer $TOKEN" \
     "http://127.0.0.1:<control port>/v1/events/wait?kind=exchange_ended&scheme=imap&timeout_ms=10000"
```

Recipe: `StandInControlEndpointRecipeTests`. The endpoint has no hooks (decision 5 keeps them in `Surl.Api`).

## `Surl.Mcp`, for AI agents

`surl-mcp` offers `start_server`, `list_servers`, `stop_server`, `get_exchanges`, `get_events`,
`wait_for_event`, `seed_mail`, `put_file`, `delete_file`, `publish_message`, `set_key`, `add_ldap_entries`,
`add_dns_zone`, `add_account`, `remove_account`, `set_account_password`, `add_proxy_account`,
`remove_proxy_account`, `set_proxy_account_password`, `add_tls_srp_account`, `remove_tls_srp_account`,
`set_tls_srp_account_password`, `list_mailboxes`, `list_messages`, `read_message`,
`read_file`, `list_files`, `browse_queue`, `read_retained_message`, `read_log`, `read_key`, `list_keys`, `read_directory`, `read_zone` (the read-back tools; `get_events`
and `wait_for_event` also take `store` and `name`), `reset_server`,
`snapshot_server`, `restore_server`, `add_fault`, `list_faults` and `remove_fault`, and the `surl://aihelp`
resources. See [`Application/Surl.Mcp/README.md`](../../Application/Surl.Mcp/README.md).

## Recipes

Each is an `Integration` test class in `Conformance/Surl.Conformance.UnitTests`; upstream curl plays the
code under test, and a test is inconclusive where no pinned build is installed.

- **Email round trip** - `StandInMailRoundTripRecipeTests.cs`: `SendOverSmtp_ThenReadOverImapAndPop3_ReturnsTheSameMessage`,
  `SeedMessage_ThenReadOverPop3_ReturnsTheSeededMessage`, `ResetAsync_AfterSeeding_LeavesTheMailboxEmptyForTheNextTest`.
- **File upload** - `StandInFileUploadRecipeTests.cs`: `UploadOverFtp_ThenFetchOverHttp_ReturnsTheUploadedBytes`,
  `UploadOverSftp_ThenFetchOverHttp_ReturnsTheUploadedBytes`.
- **Injected faults** - `StandInFaultRecipeTests.cs` (tests named above).
- **Parallel instances** - `StandInParallelInstancesRecipeTests.cs` (tests named above).
- **Publish and consume** - `StandInPublishAndConsumeRecipeTests.cs`:
  `PublishOverMqtt_ThenConsumeOverNats_ReceivesTheExactPayload`, `PublishOverMqtt_ThenConsumeOverStomp_ReceivesTheExactPayload`,
  `PublishOverMqtt_ThenConsumeOverRedis_ReceivesTheExactPayload`.
  Upstream curl speaks none of NATS, STOMP or Redis, so a test-local `TcpClient` writes those protocols' frames.
- **Control endpoint** - `StandInControlEndpointRecipeTests.cs`: `SeedMail_ThenReadOverImap_ReturnsTheSeededMessage`,
  `RefuseLoginFault_ThenReadOverImap_FailsWithLoginDeniedUntilTheFaultIsRemoved`,
  `WaitForEvent_AfterTheImapExchange_ReturnsThatExchange`, `Reset_AfterSeeding_LeavesTheMailboxEmpty`,
  `ControlRequestWithoutTheToken_IsRefusedWith401`.
