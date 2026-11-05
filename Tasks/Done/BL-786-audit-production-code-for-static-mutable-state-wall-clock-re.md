---
id: BL-786
title: Audit production code for static mutable state, wall-clock reads and unseeded randomness and file a fix task per area
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-822]
touches: [Tasks/Backlog]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-786 — Audit production code for static mutable state, wall-clock reads and unseeded randomness and file a fix task per area

## Goal

Every place in production code that would make an embedded Surl non-deterministic, or unsafe to run as several
instances in one process, is found, classified against BL-822's ADR decision 6, and has a fix task on the board.

## Context

- Decision: BL-822's ADR decision 6 (determinism): which randomness may be seeded (non-secret choices) and which
  never (keys, nonces, challenges, tokens), how the seed is given, and that nothing holds static mutable state.
- Scope: every production project in `Surl.slnx` - each `*.UnitLibrary`, `Surl.Console`, `Surl.Api`, `Surl.Mcp`.
  Not test projects. Search at least for: non-`readonly` static fields and properties with setters; `static
  readonly` collections or objects that are mutated; `DateTime.Now`/`UtcNow`, `DateTimeOffset.Now`/`UtcNow`,
  `Environment.TickCount`, `Stopwatch` not fed by `TimeProvider`; `Random.Shared`, `new Random(`,
  `Guid.NewGuid()`, `RandomNumberGenerator` used for a non-secret value; process-wide caches.
- Leads already seen on 2026-11-05 (verify each): `AzureReply.NewRequestId`
  (`Surl.Extended.Protocol.AzureStorage`), `SignalRHubConnections.NewId`, `Amqp0BrokerConnection.GenerateName`,
  `Amqp1ContainerOptions.NewContainerId` - non-secret IDs from `RandomNumberGenerator`, candidates for the seedable
  source; `RandomNtlmServerChallengeSource.Instance`, `RandomSaslNonceSource.Instance`,
  `CryptographicCoapRandomSource.Instance` - stateless secret sources, likely fine as singletons but confirm.
- How to file: one `feature` task per library (or per tight group of libraries in one category folder) with
  `task-board.ps1 new`, `-DependsOn BL-822`, exact `-Touches`, each naming the file, member, finding, the fix the
  ADR's rule calls for and a named test that shows two instances (or two seeds) behave as the rule says. If the
  ADR adds a seed option or a seedable random source, file the task that creates it first and make the fix tasks
  depend on it.
- This task changes no code (docs pipeline); its record is the filed tasks and the Notes below.

## Acceptance criteria

- [x] This task's Notes list every finding as `file - member - kind (static state, wall clock, seedable random,
      secret random kept) - task ID or "no change, because ..."`.
- [x] Every finding that needs a change has a filed `Backlog` task as described in Context, and none of them
      duplicates a task already on the board (depend on it instead).

## Notes

Audit of 2026-11-05 over every production project (`Application`, `Conformance`, `Cryptography`, `Foundation`,
`Protocols`, test projects excluded; the repository-root `Run-*.cs` file-based apps and `.github/gource` are not
in `Surl.slnx`'s production set and were left out). Searches: `DateTime(Offset).Now/UtcNow/Today`,
`Environment.TickCount`, `Stopwatch`, `Random.Shared`, `new Random(`, `Guid.NewGuid`, every
`RandomNumberGenerator` use, `TimeProvider.System`, non-`readonly` static fields, static properties with setters,
`ThreadStatic`/`AsyncLocal`, static `Lazy`/concurrent collections, and every `static readonly` collection or
array checked (PowerShell scan) for a write anywhere in its folder.

Decisions taken here (ADR-0124 decision 6 already decides the rule; these apply it):
- The seedable source and the seed option come first: BL-839 (`IIdentifierRandom`, its two sources,
  `ExchangeContext.Identifiers`), then BL-840 (`--random-seed`, `SurlServerOptions.RandomSeed`, binding per
  instance). Per-exchange fixes depend on BL-839 only, because their tests can hand a `SeededIdentifierRandom` to
  an `ExchangeContext` directly; server-life fixes (BL-850) also depend on BL-840, which exposes the instance's
  server-life source.
- Identifiers drawn when a server is created take an `IIdentifierRandom` in the constructor; those drawn per
  exchange use `ExchangeContext.Identifiers`. This keeps per-exchange fixes out of `Surl.Composition`, so they run
  in parallel.
- Guid temporary file names are left alone: a client never sees them, and seeding them would make two instances
  sharing a data directory collide.
- QUIC connection IDs, RTSP session IDs, MCP session IDs and the POP3 APOP timestamp are classed as secret: each
  either admits a client to a session or is a challenge (ADR-0124 lists session and connection tokens and
  challenges as never seeded).
- Static `readonly` lookup tables written only at type initialisation (e.g. `HpackHuffman`'s static constructor)
  count as immutable values; the one public array a caller could write (`GopherMenu.NothingServedHere`) gets a
  Low task.

Findings (`file - member - kind - outcome`):
- `Foundation/Surl.Protocol.Abstractions`, `Foundation/Surl.Core` - (missing) `IIdentifierRandom` - seedable random - BL-839.
- `Application/Surl.Cli`, `Surl.Api`, `Surl.Composition` - (missing) `--random-seed` / `RandomSeed` - seedable random - BL-840.
- `Surl.Extended.Protocol.S3/S3ProtocolServer.cs:81`, `S3RequestAnswerer.cs:51,95` - request ID - seedable random - BL-841.
- `Surl.Extended.Protocol.AzureStorage/AzureReply.cs:78` - `NewRequestId` - seedable random - BL-841.
- `Surl.Extended.Protocol.Coap/CryptographicCoapRandomSource.cs` (`Instance`) via `CoapFlowSession.cs:59,299`,
  `CoapResourceAnswerer.cs:293` - message ID, retransmission jitter, posted name - seedable random (not secret,
  contrary to the lead) - BL-842.
- `Surl.Extended.Protocol.SignalR/SignalRHubConnections.cs:146` - `NewId` - seedable random for a `connectionId`
  beside a token; secret random kept for the token and a token-less `connectionId` - BL-843.
- `Surl.Extended.Protocol.Amqp0/Amqp0BrokerConnection.Exchanges.cs:58` - `GenerateName` - seedable random - BL-844.
- `Surl.Extended.Protocol.Amqp1/Amqp1BusNodeDirectory.cs:53` - dynamic queue name - seedable random - BL-844.
- `Surl.Extended.Protocol.Amqp1/Amqp1ContainerOptions.cs:15,21,43` - `Default` holding `NewContainerId()` - static
  state and seedable random - BL-844.
- `Surl.Extended.Protocol.Mqtt5/Mqtt5BrokerState.cs:213` - `NewAssignedClientIdentifier` - seedable random - BL-845.
- `Surl.Extended.Protocol.Stomp/StompServerConnection.cs:130` - CONNECTED `session` header - seedable random - BL-846.
- `Surl.Extended.Protocol.Kafka/KafkaMetadataAnswerer.cs:59` - `ClusterId` - seedable random - BL-850.
- `Surl.Extended.Protocol.Nats/NatsServer.cs:86` - `ServerId` - seedable random - BL-850.
- `Surl.Protocol.Ldap/LdapProtocolServer.cs:104` (used by `SurlServerComposition.cs:365,471`) - `LdapDirectory`
  on `TimeProvider.System` - wall clock - BL-847.
- `Surl.Networking/StreamConnection.cs:78` via `TcpConnectionListener.cs:164`, `UnixSocketConnectionListener.cs:110`,
  `SocketListenerFactory` - peek and close timing on `TimeProvider.System` - wall clock - BL-848.
- `Surl.Protocol.Gopher/GopherMenu.cs:19` - `public static readonly byte[] NothingServedHere` - static state
  (writable array) - BL-849.
- `Surl.Networking/SocketDataConnectionOpener.cs:35`, `SocketOriginConnector.cs:32` - `?? TimeProvider.System`
  default - wall clock - no change for the opener, because the composition passes its `TimeProvider`
  (`ComposedServing.cs:112`); the connector is checked in BL-848.
- `Surl.Protocol.Abstractions/InMemoryConnection.cs:75` - `peekClock ?? TimeProvider.System` - wall clock - no
  change, because no production code constructs it; tests pass their clock.
- `Surl.Protocol.Ftp/FtpUpstreamTestCaseServer.cs:54` - `InMemoryContentFileSystem(TimeProvider.System)` - wall
  clock - no change, because only the conformance replay of upstream curl's test cases constructs it, never a
  stand-in instance.
- `Surl.Api/SurlServerOptions.cs:148`, `Surl.Console/Program.cs:51`, `Surl.Mcp/SurlMcpHostSeams.cs:20` -
  `TimeProvider.System` - wall clock - no change, because these are the hosts' defaults, the one place the clock is
  chosen and injected.
- `Surl.Content/ContentStore.cs:1088`, `ObjectMetadataIndex.cs:420`, `Surl.MailStore/MailStoreFiles.cs:162,188`,
  `Surl.MessageBus/MessageBusFiles.cs:225` - `Guid.NewGuid` temporary names - unseeded random - no change, because
  no client sees them and they must differ across instances sharing a directory.
- `Surl.Authentication/RandomNtlmServerChallengeSource.Instance`, `RandomSaslNonceSource.Instance`,
  `CryptographicSecretComparer.Instance` - stateless singletons, secret random kept - no change, because they hold
  no fields (decision 6 allows them).
- `Surl.Authentication/AccountBook.cs:24-42,156,283`, `AuthorizedKeyBook.cs:16`, `DigestNonceBook.cs:26,42`,
  `NtlmPasswordHashes.cs:63` - dummy hashes, salts, digest nonce key and nonces - secret random kept - no change.
- `Surl.Composition/RandomKerberosRandomSource.cs`, `Surl.Protocol.Smb/SmbSystemChallengeSource.cs`,
  `Surl.Protocol.Ssh/SshSystemRandomSource.cs` - Kerberos, SMB challenge, SSH randoms - secret random kept - no change.
- `Surl.Dtls` (`DtlsCookieSecrets`, `DtlsEphemeralKey`, `DtlsServerSession.cs:318,584`), `Surl.Tls13`
  (`TlsKeyExchange`, `TlsServerHandshake`) - keys, randoms, cookie secrets - secret random kept - no change.
- `Surl.Quic/QuicServerConnection.cs:346`, `QuicDatagramFlowServer`, `Surl.Protocol.Http/Http3ProtocolServer.cs:78`,
  `Surl.Extended.Protocol.DnsQuery/DnsOverQuicProtocolServer.cs:73` - QUIC connection IDs and handshake randoms -
  secret random kept - no change.
- `Surl.Protocol.Rtsp/RtspRequestResponder.Sessions.cs:222` - RTSP `Session` ID - secret random kept (it admits a
  client to the session) - no change.
- `Surl.Extended.Protocol.McpServer/McpHttpSessionTable.cs` - `Mcp-Session-Id` - secret random kept - no change.
- `Surl.Protocol.Pop3/Pop3Session.cs:101` - APOP timestamp - secret random kept (a challenge) - no change.
- `Surl.Hpack/HpackHuffman.cs:47-59` - static arrays filled by the static constructor - static state - no change,
  because nothing writes them after type initialisation.
- Every other `static readonly` table and every static getter-only property - immutable values - no change: the
  scan found no write to any of them after initialisation, no non-`readonly` static field, no static setter, no
  `ThreadStatic`/`AsyncLocal` and no process-wide cache in production code.
- No `DateTime.Now`/`UtcNow`, `DateTimeOffset.Now`/`UtcNow`, `Environment.TickCount`, `Stopwatch`, `Random.Shared`
  or `new Random(` in production code.

Duplicates checked: no Backlog, Doing, Blocked or Deferred task named `IIdentifierRandom`, `--random-seed` or any
of these members (BL-823 says decision 6 is BL-786's to file). BL-846 shares Stomp with BL-532 in Doing; the board
runs them one after the other.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Production code audited against ADR-0124 decision 6; 40 findings classified and 12 fix tasks filed (BL-839 to BL-850)
