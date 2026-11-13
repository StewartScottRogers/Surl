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

One instance, one mail store, one content store, one message bus and one account store (ADR-0100). Mail
sent over SMTP is read over IMAP or POP3; a file uploaded over FTP or SFTP is fetched over HTTP; a message
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

A seed goes through the path a client would write by, so names, limits and events apply, and a seed never
trips a fault rule; one a client would be refused throws `SurlSeedRefusedException`. A snapshot is an
in-memory copy that only its own server restores; with `DataDirectory` set, reset and snapshot throw
`InvalidOperationException` so a fixture never empties a real directory. Recipes:
`StandInMailRoundTripRecipeTests.SeedMessage_ThenReadOverPop3_ReturnsTheSeededMessage` and
`ResetAsync_AfterSeeding_LeavesTheMailboxEmptyForTheNextTest`.

## Faults

A `SurlFault` is one `--fault` item: `Delay`, `DropConnection`, `RefuseLogin`, `ProtocolError`, `OutOfSpace`,
narrowed by `Listener`, `Scheme`, `Account`, `Path`, `Command`, `Stage`, `AfterBytes` and `Times`. Give them
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

`surl --control http://127.0.0.1:0 --control-token-file control.token <listen URLs>` serves HTTP and JSON
under `/v1/` on loopback, guarded by the bearer token (the file's first line, 32 to 1024 visible ASCII
bytes); `--control` without a token file is refused, and a request without the token gets `401`. Routes:
`GET /v1/events`, `GET /v1/events/wait`, `GET /v1/exchanges`, `GET` and `POST /v1/faults`,
`DELETE /v1/faults/<id>`, `POST /v1/mail?to=<mailbox>`, `PUT` and `DELETE /v1/files/<path>`,
`POST /v1/messages`, `POST /v1/accounts`, `DELETE /v1/accounts/<name>`, `POST /v1/reset`,
`POST /v1/snapshots` and `POST /v1/snapshots/<id>/restore`. With curl, as the recipe runs it:

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
`wait_for_event`, `seed_mail`, `put_file`, `publish_message`, `add_account`, `reset_server`,
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
  `PublishOverMqtt_ThenConsumeOverNats_ReceivesTheExactPayload`, `PublishOverMqtt_ThenConsumeOverStomp_ReceivesTheExactPayload`.
  Upstream curl speaks neither NATS nor STOMP, so a test-local `TcpClient` writes those protocols' frames.
- **Control endpoint** - `StandInControlEndpointRecipeTests.cs`: `SeedMail_ThenReadOverImap_ReturnsTheSeededMessage`,
  `RefuseLoginFault_ThenReadOverImap_FailsWithLoginDeniedUntilTheFaultIsRemoved`,
  `WaitForEvent_AfterTheImapExchange_ReturnsThatExchange`, `Reset_AfterSeeding_LeavesTheMailboxEmpty`,
  `ControlRequestWithoutTheToken_IsRefusedWith401`.
