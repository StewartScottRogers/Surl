# ADR-0124 — Surl is a stand-in for tests

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Stewart, 2026-11-05 (the principles); Claude under Stewart's delegation (root
  `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-822 (the design).
- **Builds on:** [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) (upstream curl is the only
  oracle), ADR-0004 section 5 (every byte reaches `IExchangeLog`), ADR-0032 (secure by default), ADR-0046
  (`--aihelp`), [ADR-0099](ADR-0099-one-server-composition-for-surl-and-surl-api-and-surl-apis-programming-interface.md)
  (`Surl.Composition`, `Surl.Api`), [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  (the shared stores), [ADR-0101](ADR-0101-surls-configuration-surface-the-config-file-repeatable-item-options-and-one-configuration-model.md)
  (one configuration model and its item grammar), ADR-0102 (what the store options mean) and
  [ADR-0103](ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md)
  (`Surl.Mcp`).
- **Amends:** ADR-0099 decision 7 (the exchange record gains its listen URL; decision 1 below) and
  ADR-0103 decision 3 (new and widened tools; decision 8 below).
- **Acted on by:** BL-785 to BL-821 and BL-823, and the extended-server follow-ups listed under
  Consequences.

## Context

Stewart set the principles on 2026-11-05 (root `CLAUDE.md`, "Surl is a stand-in for tests"): besides
answering upstream curl, Surl is a real server that other people's integration tests run against in place
of the real mail server, bucket, broker or file site, often several protocols at once. Every design asks
how a test would use it, alone or with another protocol, and whether it extends without a rewrite. The
principles are recorded here, not reopened:

- **Embeddable** - starts inside a test through `Surl.Api`, on port 0, in memory, many instances side by
  side, no static mutable state.
- **Composable** - any mix of protocols in one instance, same-job protocols sharing one store.
- **Observable** - each instance publishes every exchange and store change on one event stream, and
  recording, assertions, `Surl.Mcp`, the control endpoint and hooks all subscribe to it.
- **Controllable** - stores can be seeded, reset, snapshotted and restored.
- **Fault-injectable** - delay, drop, refuse a login, answer a chosen error, run out of space, per
  listener, account or path, configured like everything else.
- **Deterministic** - time from the injected `TimeProvider`, randomness seedable.
- **Open for extension** - protocols, stores, faults and hooks plug into explicit, AOT-safe extension
  points, never a central switch.
- **Configuration as data** - one configuration model for the command line, `-K`, `Surl.Api`, `Surl.Mcp`
  and the control endpoint; it only grows.
- All of it **beside secure by default**: anything that widens what a peer or a test may do is off until
  asked for, and warned.

What exists and is built on, not replaced: `Surl.Composition` composes one `ComposedServing` per `surl`
run or `Surl.Api` server (ADR-0099); `Surl.Api` records exchanges through an internal
`RecordingExchangeLogFactory` with no listen URL, because `IExchangeLogFactory.Create` receives none
(ADR-0099 decision 7); `Surl.Mcp`'s `get_exchanges` reads that record (ADR-0103 decision 3); the four
shared stores `ContentStore`, `MailboxStore`, `MessageBus` and `AccountBook` (ADR-0100) are configured
through `SurlCommandLine` (ADR-0101); `ExchangeContext` is what every server is told about one exchange;
`Surl.HttpServing` is a horizontal HTTP/1.1 serving library that references no protocol server.

Nothing here is a byte upstream curl sends or expects: these are test-facing surfaces. Where a decision
changes what a protocol server answers (a chosen error, a dropped connection), the answer is one the
protocol's specification already allows, and the task that wires it proves upstream curl's reaction with a
pinned build (ADR-0003). No package is needed: channels, JSON source generation and the HTTP serving are in
the base class library or already in the solution.

## Decision

### 1. The event stream

**Contract** - `Foundation/Surl.Protocol.Abstractions.UnitLibrary`, built by BL-823:

```csharp
public interface ISurlEventPublisher
{
    bool IsPublishingBytes { get; }            // true while at least one subscription asked for bytes
    void Publish(SurlEvent surlEvent);         // never blocks, never throws into the caller
}

public abstract record SurlEvent
{
    public long Sequence { get; init; }        // stamped by the stream: 1, 2, 3 ... per stream, no gaps
    public DateTimeOffset At { get; init; }    // stamped by the stream from its TimeProvider
}

public sealed record ExchangeStartedEvent(long ExchangeId, string ListenUrl, string Scheme, EndPoint Client) : SurlEvent;
public sealed record ExchangeBytesEvent(long ExchangeId, ExchangeDirection Direction, ReadOnlyMemory<byte> Bytes) : SurlEvent;
public sealed record ExchangeEndedEvent(long ExchangeId, long ReceivedByteCount, long SentByteCount) : SurlEvent;
public sealed record LoginJudgedEvent(long? ExchangeId, string Scheme, string Account, string Mechanism, bool IsAccepted, bool IsRefusedByFault) : SurlEvent;
public sealed record StoreChangedEvent(StoreKind Store, StoreChange Change, string Name, long? ByteCount, long? ExchangeId) : SurlEvent;
public sealed record FaultAppliedEvent(string RuleId, FaultKind Kind, FaultStage Stage, long? ExchangeId, string Target) : SurlEvent;
public sealed record HookFailedEvent(string HookId, HookFailure Failure, string Message) : SurlEvent;

public enum ExchangeDirection { Received, Sent }
public enum StoreKind { Content, Mail, Bus, Accounts }
public enum StoreChange { Written, Deleted, Delivered, Published, Enqueued, Acknowledged, AccountAdded, AccountRemoved, Seeded, Reset, Restored }
public enum HookFailure { Threw, TimedOut }
```

`ExchangeContext` gains `Events` (`ISurlEventPublisher`, default a null publisher that publishes nothing
and reports `IsPublishingBytes` false) and `Faults` (decision 4), both `init`, so every existing
`new ExchangeContext(...)` compiles and behaves as before. `StoreChangedEvent.Name` is the path, mailbox,
topic or queue, or account name the change touched. `LoginJudgedEvent` never carries a password, hash,
token or any other secret. The library's test doubles gain a recording publisher and a scripted fault plan.

**Implementation** - `SurlEventStream` in `Foundation/Surl.Core.UnitLibrary`, built by BL-787:

- **Ordering.** One lock stamps `Sequence` and `At` (with `with`) and hands the event to every
  subscription, so every subscriber sees one total order. The events of one exchange keep the order in
  which they happened (bytes in the order they crossed the connection). Publishers construct events
  with `Sequence` 0 and `At` default.
- **Subscriptions.** `Subscribe(SurlEventSubscriptionOptions)` returns a `SurlEventSubscription`
  (`IAsyncDisposable`, `ReadAllAsync(CancellationToken)`, `DroppedEventCount`), backed by a bounded
  `System.Threading.Channels` channel. Options: `Capacity` (default 65536 events, 1 to 1048576),
  `IncludeBytes` (default false) and an optional predicate run at publish time. A subscription sees only
  events published after it was made; there is no replay.
- **Past the bound** the oldest undelivered event is dropped (`BoundedChannelFullMode.DropOldest`) and
  `DroppedEventCount` grows, so a slow subscriber loses events and never slows a server; the gap shows in
  `Sequence`. A recorder that must lose nothing subscribes with a capacity to match.
- **Bytes are published only while someone asked for them.** The engine copies a chunk into an
  `ExchangeBytesEvent` only when `IsPublishingBytes` is true. Every other event is always published; with
  no subscriber that costs one stamp.
- **Time** is the stream's injected `TimeProvider`, the instance's. No wall clock.
- **Publish points.** `ServingEngine` publishes exchange start (with the listen URL as the instance
  reports it, bound port included), bytes and end (BL-787, closing ADR-0099 decision 7's gap); the stores
  publish their changes (mail BL-788, content BL-789, bus BL-790; accounts BL-802);
  `AuthenticationPolicy` publishes each judged login (BL-791); the fault plan publishes each application
  (BL-792); hooks publish their failures (BL-811).
- **One stream per `ComposedServing`**, created by `SurlServerComposition.ComposeAsync` and exposed as
  `ComposedServing.Events` (BL-803); never static, so two instances never see each other's events.
- **Subscribers:** `Surl.Api`'s exchange record, rebuilt on a subscription with `IncludeBytes` (BL-806),
  and its queries and waits (BL-807); `Surl.Mcp` (BL-809); the control endpoint (BL-814); hooks (BL-811).
  The levelled log, `--trace` and `--trace-ascii` stay on `IExchangeLog` unchanged.
- **The record gains its listen URL** (amends ADR-0099 decision 7): `SurlExchangeRecord` gains
  `ListenUrl` and `Scheme`; the bullet "No listen URL on a record" no longer holds. The record keeps its
  bounds and stays off unless `RecordExchanges` is set.
- **`Surl.Api`'s queries** (BL-807): `SurlEventQuery` (`Scheme`, `ListenUrl`, `Account`, `Kind`,
  `Since`, `Until`, `AfterSequence`), `FindExchanges`, `FindEvents`, `WaitForExchangeAsync` and
  `WaitForEventAsync`. `Surl.Api` keeps at most `MaxRecordedEvents` events without their bytes (default
  10000, oldest dropped). A wait that times out throws `TimeoutException` saying what was asked for and how
  many events were seen while waiting; the wait's clock is the injected `TimeProvider`.

### 2. Extension points

Every extension point is an explicit registration - a list entry or a delegate - and AOT-safe: no
reflection, no assembly scanning, no attribute read at run time.

- **Protocol servers** (BL-804). `Surl.Composition` gains `ProtocolServerRegistration(IReadOnlyList<string>
  Schemes, Func<ComposedProtocolServices, IProtocolServer> Create)`. `ComposeProtocolServers` keeps its
  name and result and iterates an ordered list `ProtocolServerRegistrations.Original` instead of building
  servers inline; the implicit-TLS second registrations stay entries of the same list. The extended servers
  move onto the same record type in `ExtendedProtocolServers.Registrations`, still gated by
  `SURL_EXTENSIONS` (ADR-0083). A new protocol joins by adding one entry. `ComposedProtocolServices` holds
  the composed content store, mail store, message bus, authentication policy, `TimeProvider` and the
  services `ExtendedProtocolServerServices` holds today.
- **A test's own protocol server** (BL-813, on BL-804). `SurlServerOptions.ProtocolServers` takes
  `SurlProtocolServer(string Scheme, Func<SurlProtocolServices, IProtocolServer> Create)`, where
  `SurlProtocolServices` is `Surl.Api`'s public view of the content store, mail store, message bus,
  authentication policy and `TimeProvider`. **Add only:** a scheme any original or extended server
  claims, turned on or not, is refused with `ArgumentException` at `StartAsync`, so a test can never
  replace a server whose answers upstream curl judges. A custom scheme's listen URL is given in
  `ListenUrls` and must name its port (0 allowed); `Surl.Api` takes those URLs out before `Surl.Cli`
  parses the rest, reads each with `System.Uri` into a `ListenUrl` (host as for every listen URL, IPv6
  brackets removed), and hands the composition each registration with its listen URLs through a new
  composition input, `AdditionalProtocolServers` (BL-804 adds the input; no `Surl.Cli` change). The
  command line cannot name a custom scheme. Its exchanges are published by the engine like any other.
- **Faults** are a closed set of five kinds (decision 4). A new kind needs an ADR; test-specific
  behaviour comes through hooks (decision 5), which can choose only among the five.
- **Stores are not replaceable.** A test may not supply its own content store, mail store, bus or
  account book. The four are concrete classes several servers share with invariants (paths, quotas,
  UIDs, ADR-0100's rules); a replaceable store would need an interface across every server that reads it,
  which is the rewrite the principle forbids. A test shapes a store's contents by seeding, resetting and
  restoring it (decision 3). A later ADR may open this if a real use appears.

### 3. Seed, reset, snapshot and restore

Each store gains four operations (mail BL-799, content BL-800, bus BL-801, accounts BL-802), exposed
together through `ComposedServing.Stores` (`ComposedStores`, BL-805), then by `Surl.Api` (BL-810),
`Surl.Mcp` (BL-812) and the control endpoint (BL-814, BL-816).

- **Seed** writes through the same path a protocol uses - delivery for mail, an upload for content,
  publish or enqueue for the bus, `AccountBook` add for accounts - so quotas, names and events apply as
  they would to a client. **A seed never consults the fault plan**: a test prepares state without
  tripping its own out-of-space rule. Each seed publishes `StoreChangedEvent` with `Change` `Seeded`.
- **Reset** returns a store to its configured state:
  - mail: the configured owners, mailboxes, aliases, quotas and shared folders exist, holding no
    messages; every mailbox's `UIDVALIDITY` changes;
  - content: the configured shares and buckets exist as empty folders, plus the files a host supplied at
    start (`Surl.Api`'s `Files`, `Surl.Mcp`'s `files`); object metadata is dropped;
  - bus: the configured topics, queues, maps and access rules stay, holding no messages, no retained
    messages and empty logs;
  - accounts: exactly the accounts `--user`, `--user-file`, `--mailbox` and the groups configured at
    start; accounts added later are removed.
- **Snapshot** is an opaque, in-memory deep copy of a store's contents and metadata (mail UIDs, flags and
  `UIDVALIDITY`; object metadata and ETags; retained messages, queue contents and logs; accounts as
  password hashes only, never a clear password). `ComposedSnapshot` holds one snapshot per store. A
  snapshot is bounded: one whose stores together exceed `MaxSnapshotBytes` (default 256 MiB) is refused,
  never truncated. A snapshot carries its instance's identity.
- **Restore** replaces a store's contents with a snapshot's. A snapshot from another instance is refused.
  Every mailbox's `UIDVALIDITY` changes on restore, as on reset, so an IMAP client resynchronises.
- **Order across stores** for reset and restore of a whole instance: content, mail, bus, then accounts
  (ADR-0100 decision 1's load order, accounts last), one store at a time; the first failure stops it and
  is reported naming the store.
- **Live sessions.** Reset and restore wait for no one: each takes its store's own lock and swaps the
  contents. Open sessions keep their connections and see the new contents on their next command; an
  upload in progress fails at its commit with the store's ordinary write failure; on the bus, live
  subscriptions stay and unacknowledged deliveries are dropped, not redelivered; a removed account's open
  sessions run on until they end (a login is judged when it is made).
- **Stores on disk are not reset, snapshotted or restored.** With `--directory`, the content store and the
  persisted mail and bus under `<path>/.surl` refuse reset, snapshot and restore with
  `InvalidOperationException` (a `409` on the control endpoint) saying the store is on disk, so a test
  fixture can never empty a real directory. Seeding a disk store is allowed: it is ordinary writing.
  Accounts are in memory in every case and are always controllable.
- **Events.** A reset or restore publishes one `StoreChangedEvent` per store (`Reset`, `Restored`), not
  one per item.

### 4. The fault model

**Kinds** (`FaultKind`): `Delay`, `Drop`, `RefuseLogin`, `Error`, `OutOfSpace`. **Stages**
(`FaultStage`): `Accept` (a connection accepted or a datagram flow's first datagram, before any byte,
TLS included), `Login` (a login being judged), `Command` (each request, command, packet or operation a
server consults on), `StoreWrite` (a store about to write).

| Kind | Stages | Applied by | Built by |
| --- | --- | --- | --- |
| `delay` | `Accept` (default), `Command` | the serving engine at accept; each server at a command | BL-792; BL-793 to BL-798 |
| `drop` | `Accept` (default), `Command`, or after `after-bytes` sent | the serving engine (abort the connection; a datagram flow stops answering) | BL-792 |
| `refuse-login` | `Login` | `AuthenticationPolicy`: refused as a wrong password is, after the same delay | BL-791 |
| `error` | `Command` | each server family | BL-793 to BL-798 |
| `out-of-space` | `StoreWrite` | each store, with the outcome it already has for a full quota | BL-788 to BL-790 |

**Contract** (BL-823): `IFaultPlan.FindAsync(FaultPoint point, CancellationToken)` returning
`ValueTask<FaultRule?>` - asynchronous because a hook may decide (decision 5); it completes synchronously
when no hook is registered. `FaultPoint` carries the stage, exchange ID, listen URL, scheme, account,
path and command known at that point. `FaultRule` carries `Id`, `Kind`, its scope and its parameters.
`ExchangeContext.Faults` defaults to an empty plan.

**The matcher** - `FaultPlan` in `Surl.Core` (BL-792), thread-safe, changeable while serving:

- Rules are tried in the order added; the first whose every given attribute matches, whose kind is
  allowed at the point's stage, and whose count is not spent, wins. An absent attribute matches anything.
- `listener` matches the listen URL as given or as bound (port 0 resolved); `scheme` matches the scheme;
  `account` matches as the account book compares names; `path` matches equal, or as a segment prefix (the
  point's path continues with `/`); `command` matches the protocol's verb case-insensitively.
- `times=<n>` is decremented atomically when the rule is applied, so `times=1` fires exactly once under
  concurrent exchanges; without `times` the rule always applies.
- At most 256 rules per instance. Rule IDs are `f1`, `f2`, ... per instance, never reused.
- Every application publishes `FaultAppliedEvent`.

**What "path" and "command" mean per family**:

| Family | `command` | `path` |
| --- | --- | --- |
| HTTP, RTSP, WebSocket | the method (`GET`, `DESCRIBE`); WebSocket the upgrade request | the request path, query excluded |
| FTP, TFTP | the command (`RETR`, `STOR`, `LIST`); TFTP `RRQ`, `WRQ` | the file path |
| SSH (SFTP, SCP), SMB | the SFTP packet name (`OPEN`, `READ`), `scp`; the SMB command (`CREATE`, `READ`) | the file path |
| SMTP, IMAP, POP3 | the command (`MAIL`, `RCPT`, `DATA`, `SELECT`, `RETR`) | SMTP the recipient address at `RCPT`; IMAP the mailbox; POP3 none |
| MQTT, LDAP | the packet type (`PUBLISH`, `SUBSCRIBE`); the operation (`search`, `add`) | the topic; the base DN |
| DICT, Gopher, TELNET | the command (`DEFINE`, `MATCH`); none | the word or database; the selector |

**Chosen errors are checked, not free text.** An `error` rule must name `scheme` or `listener`, and its
`reply` must fit that protocol's error shape, checked when the rule is read (BL-785) or added:

| Scheme | `reply` |
| --- | --- |
| `http`, `https`, `rtsp` | a status 400 to 599 and an optional reason phrase |
| `ws`, `wss` | a close code RFC 6455 section 7.4 allows on the wire - 1000 to 1014 less 1004 to 1006, and 3000 to 4999 - and an optional non-empty reason of at most 123 UTF-8 bytes (ADR-0134 decision 4, BL-903) |
| `ftp`, `ftps`, `smtp`, `smtps` | a reply code 400 to 599 and text |
| `imap`, `imaps` | `NO` or `BAD` and text |
| `pop3`, `pop3s` | text, sent after `-ERR ` |
| `tftp` | an error code 0 to 7 and text |
| `sftp`, `scp` | an SFTP status 2 to 8 and text; `scp` a message line sent after byte 1 |
| `smb`, `smbs` | an NTSTATUS error as eight hex digits |
| `mqtt`, `mqtts` | a reason code 128 to 255 (MQTT 5) - an MQTT 3.1.1 session is disconnected |
| `ldap`, `ldaps` | a resultCode 1 to 80 other than 10 and 14, and diagnostic text |
| `dict` | a status 500 to 599 and text |
| `gopher` | text, sent as a Gopher error item (`3`) |
| `telnet` | one line of text, sent and then the connection closed |
| `socks4`, `socks4a` | a reply code 91 to 93, alone, sent as the failed `CONNECT`'s `00 5B` to `00 5D` reply (BL-1061; curl exits 97, ADR-0121 row 4) |
| `socks5`, `socks5h` | a reply code 1 to 8, alone, sent as the failed `CONNECT`'s `05 <code>` reply (BL-1061; curl exits 97, ADR-0121 row 11) |

The extended servers (decision 9), each judged by its own specification (ADR-0083):

| Scheme | `reply` |
| --- | --- |
| `s3`, `az`, `abfs`, `abfss`, `gs` | a status 400 to 599 and an error code word, written in the service's own error body (`503 SlowDown`, `503 ServerBusy`) |
| `amqp`, `amqps` | an AMQP 0-9-1 reply code from the specification's exception list and text |
| `amqp1`, `amqp1s` | an AMQP 1.0 error condition symbol and a description |
| `kafka`, `kafkas` | a Kafka error code 1 to 127, in the request's own response |
| `mqtt5`, `mqtt5s`, `mqtt5+ws` | a reason code 128 to 255, as `mqtt` |
| `nats`, `natss` | text, sent as `-ERR '<text>'` |
| `coap`, `coaps` | a response code 4.00 to 5.05 |
| `mcp`, `mcps` | a JSON-RPC error code and message |
| `signalr`, `signalrs` | text, sent as the invocation's completion `error` |
| `stomp`, `stomps` | text, sent as an `ERROR` frame's `message` header, after which the connection closes (BL-1062) |
| `xmpp`, `xmpps` | a stanza error condition of RFC 6120 section 8.3.3 (for example `service-unavailable`), answered as `<error type=...>` on the matching stanza (BL-1063) |
| `dns` | an RCODE 1 to 5 |
| `redis`, `rediss` | text, sent as a RESP error (`-<text>`) |

A checked reply keeps a fault "an error the protocol allows": free text could make a server send bytes
no specification describes, and could make a refused operation look like a success. Each server sends the
reply with its own framing. A few protocol-specific answers: an FTP transfer command answered with an
error opens no data connection; an SMTP `DATA` rule answers in place of `354`, before the message; an
MQTT `PUBLISH` at QoS 0, which has no answer, ends with a `DISCONNECT` carrying the reason code (MQTT 5)
or a close (3.1.1), and at QoS 1 or 2 its `PUBACK` or `PUBREC` carries the reason code (MQTT 5).

**TLS.** An `Accept`-stage fault acts before the TLS handshake: a drop closes the TCP connection before
any `ServerHello`, a delay holds it before the handshake starts. `Command`-stage faults act inside TLS.
No fault alters the handshake itself - no certificate is swapped and TLS is never turned off.

**Delay** waits on the injected `TimeProvider`, never `Thread.Sleep`; `ms` is 0 to 600000. The server's
own idle and head timeouts do not run during it.

**Configured as data.** `--fault <item>` is repeatable, in ADR-0101 decision 3's item grammar, so `-K`
files carry it; its head is the kind, and its attributes are:

| Key | Value | Kinds |
| --- | --- | --- |
| `listener` | a listen URL | all |
| `scheme` | a scheme | all |
| `account` | an account name | all |
| `path` | a path, mailbox, topic, queue, selector or DN | all |
| `command` | a verb | `delay`, `drop`, `error` |
| `stage` | `accept` or `command` | `delay`, `drop` |
| `ms` | 0 to 600000 | `delay` (required) |
| `after-bytes` | 0 to 2^31-1 bytes sent | `drop` |
| `reply` | as the table above | `error` (required) |
| `times` | 1 to 1000000 | all |

Examples: `drop;listener=smtp://127.0.0.1:2525;times=1`, `refuse-login;account=alice`,
`delay;ms=500;stage=command;path=/slow`, `error;scheme=smtp;command=RCPT;reply=451 4.3.0 try later`,
`out-of-space;path=/uploads`. Refused as ADR-0101 decision 5 refuses a bad item (the texts after
`option <name>: `, decided by Claude under Stewart's delegation in BL-785: `unknown kind <kind>`, row 10's
`unknown key <key>`, `key <key> does not apply to <kind>`, `kind <kind> needs key <key>`,
`key <key> must be a whole number from <lowest> to <highest>`, `key stage must be accept or command`,
`unknown scheme <scheme>`, `key listener is not a listen URL`, `key listener cannot be given with key scheme`,
`key <key> cannot be given at stage accept`, `key after-bytes cannot be given with stage=command`,
`kind error needs key scheme or key listener`, `scheme <scheme> has no error shape`,
`key reply does not fit the <scheme> error shape` and `more than 256 fault rules`; none holds a value
but a kind or scheme, and the kind and keys are read in any case; `gophers` takes `gopher`'s reply shape;
`scheme` accepts every scheme surl has a server for, extended ones included, whether or not
`SURL_EXTENSIONS=0` turns them off): an unknown kind or key,
a key not allowed for the kind, `listener` with `scheme`, `account`, `path` or `command` on a `drop` or
`delay` at `accept` (none is known yet), `after-bytes` with `stage=command`, a value out of range, an `error` with neither `scheme` nor `listener`, a
`reply` that does not fit its scheme, more than 256 rules. `SurlCommandLine.Faults` holds them as
`ConfiguredFault` records in command-line order (BL-785); `--fault` is in the `testing` help category.

**Warning.** When any rule is configured, the composition writes once, with the other loosening warnings
(BL-803): `surl: warning: --fault: <n> fault rule(s) make this server misbehave on purpose`. A rule added
at run time is published only as it is applied.

**`Surl.Api`** (BL-808): `SurlServerOptions.Faults` is a list of `SurlFault` records with factories
`Delay`, `DropConnection`, `RefuseLogin`, `ProtocolError` and `OutOfSpace` and scope properties - a
spelling of `--fault` words parsed by `Surl.Cli` (ADR-0099 decision 4's rule). `SurlServer.Faults.Add`
returns the rule ID, `Remove(id)` and `Clear()` change the plan while serving, each validated by the same
reader.

### 5. Hooks

`Surl.Api` only, in process (BL-811); the control endpoint and `Surl.Mcp` offer fault rules, never
callbacks.

- **Event callbacks.** `SurlServer.OnEvent(SurlEventQuery, Func<SurlEvent, CancellationToken, ValueTask>)`
  returns an `IDisposable` registration. Each callback runs on its own subscription (decision 1, default
  capacity) on its own task, off the serving path, so a slow callback loses events as any slow subscriber
  does and never blocks a server.
- **Response overrides.** `SurlServer.Faults.AddOverride(SurlFaultScope, Func<FaultPoint,
  CancellationToken, ValueTask<SurlFault?>>)` adds a rule whose answer is computed per matching point: it
  sits in the fault plan's order like any rule and reaches servers through `ExchangeContext.Faults`, so no
  server changes. It can return only a `SurlFault` - one of the five kinds - or null for "no fault". It
  therefore cannot accept a refused login, turn TLS off, reach outside the served root or widen any option.
- **Time limit.** An override has `HookTimeLimit` (default 1 second, 1 ms to 30 seconds) on the injected
  `TimeProvider`; past it, the point is served as if no hook were registered and `HookFailedEvent`
  (`TimedOut`) is published.
- **A throwing hook** is caught; `HookFailedEvent` (`Threw`, the exception's message) is published; the
  point is served as if no hook were registered (an override) or the event is skipped (a callback); the
  hook stays registered.

**Amendment (BL-811, recorded by BL-969). Decided by Claude under Stewart's delegation.** Building hooks
settled these choices, which `Application/Surl.Api/README.md`, section "Hooks", states for users:

- **An override's answer applies to the point it was asked at.** Its own scope (`Listener`, `Scheme`,
  `Account`, `Path`, `Command`) and its `Times` are ignored; an `error` answer takes the point's scheme, so
  its reply has the shape that scheme's server sends. The answer is then read by the same reader as
  `surl --fault`.
- **An answer that cannot be applied is a failure, not a fault.** An answer `surl` refuses, or one whose
  kind cannot act at the point's stage, is published as `HookFailedEvent` (`Threw`, the refusal's message)
  and ignored: the point is served as if the override were not there.
- **Hook IDs.** An override's hook ID is its fault rule ID, `f<n>`, from the plan's one sequence, and is
  what `SurlServer.Faults.Remove` takes; an event callback's is `h<n>`, from the server's own sequence.
- **Scope.** A `SurlFaultScope` with no `Stage` is asked at every stage, as every other unset scope
  property matches anything.
- **The plan's limit and listing.** Overrides count toward the plan's 256 rules (`FaultPlan.MaxRules`) and
  are not listed in `FaultPlan.Rules`, which holds only rules with a fixed answer.
- **No feedback loop.** A callback that throws on its own `HookFailedEvent` is not published again, so a
  callback that always throws cannot feed itself; nor is a failure published once its registration is
  disposed.
- **Stopping.** A callback's `CancellationToken` is cancelled when its registration is disposed, and the
  server disposes every callback registration when it stops.

### 6. Determinism

- **Time.** Every time-dependent path uses the instance's injected `TimeProvider`; no `DateTime.Now`,
  `UtcNow`, `Environment.TickCount` or unfed `Stopwatch` in production code.
- **What may be seeded:** non-secret choices only - generated identifiers and names that grant nothing,
  such as AMQP generated queue names, request IDs, NATS `server_id`, AMQP 1.0 container IDs, XMPP stream
  IDs, a generated resource, message IDs.
- **What is never seeded:** keys, nonces, challenges, salts, session and connection tokens, cookies, TLS
  and SSH randoms, Kerberos material, a Socket.IO `sid` or SignalR `connectionToken` - anything that grants
  access or protects a secret. These stay on `RandomNumberGenerator`.
- **The seedable source.** `IIdentifierRandom` (`NextBytes(Span<byte>)`) in `Surl.Protocol.Abstractions`,
  reaching servers through `ExchangeContext.Identifiers` and the extended servers' services, with
  `CryptographicIdentifierRandom` (the default) and `SeededIdentifierRandom` in `Surl.Core`. A seeded
  instance derives each exchange's sequence from the seed and the exchange ID, so a test whose exchanges
  happen in the same order gets the same identifiers every run, however other exchanges interleave.
- **The seed's spelling.** `--random-seed <n>` (0 to 2^63-1) on the command line and in `-K` files, in
  the `testing` help category, and `SurlServerOptions.RandomSeed` as its spelling (ADR-0099 decision 4).
  No warning: it widens nothing, because nothing secret is seeded. Unseeded is the default.
- **No static mutable state.** A static field may hold only an immutable value or a stateless object
  (a secret random source with no fields is fine as `Instance`).
- BL-786 audits production code against this decision, files the task that creates `IIdentifierRandom`
  and `--random-seed` first, and files one fix task per area depending on it.

### 7. The loopback control endpoint

For test suites not written in .NET: HTTP and JSON on loopback, off by default.

- **Library.** `Application/Surl.Control.UnitLibrary` (namespace `Surl.Control`) with
  `Application/Surl.Control.UnitTests` (BL-814). It references `Surl.Protocol.Abstractions` (the event
  records) and `Surl.HttpServing` (it implements `IHttpRequestHandler`), and no protocol server and not
  `Surl.Composition`. It defines `IControlTarget` - query and wait for events, the fault plan, the store
  control - which `Surl.Composition` implements over `ComposedServing` and binds (BL-816). JSON is
  `System.Text.Json` source generation; snake_case names.
- **Options** (BL-815). `--control <listen URL>`: scheme `http` only; host `localhost`, an IPv4 address
  in `127.0.0.0/8` or `::1`; port 0 allowed. `--control-token-file <file>`: the token is the file's first
  line without its line end, 32 to 1024 visible ASCII bytes. **`--control` without
  `--control-token-file` is refused** - no token is generated, because printing it would print a secret
  and writing it would be a write nobody asked for. Refusals before anything starts, `FailedInit` (2):
  `surl: (2) --control must listen on loopback (127.0.0.0/8, ::1 or localhost): <url>`,
  `surl: (2) --control needs http, not <scheme>`, `surl: (2) --control needs --control-token-file`,
  `surl: (2) --control-token-file without --control`. At start, an unreadable token file is
  `CouldNotReadFile` (37) and a token of the wrong length or with a byte outside visible ASCII
  `FailedInit` (2), before any listener binds (BL-816). Both options are in the `testing` help category;
  the `--aihelp` topic is `control`, and its example is the refusal of `--control http://0.0.0.0:0`.
- **Start.** The control listener binds through the same `IListenerFactory` beside the protocol
  listeners, does not count toward `--max-connections`, and takes at most 8 connections of its own. Its
  status line is `Control endpoint listening on <bound URL>`, beside the `Listening on` lines. The
  warning: `surl: warning: --control: a test control endpoint on <bound URL> can change this server's
  files, mail, messages, accounts and faults`. Its own exchanges are not published on the stream it
  serves.
- **Every request** needs `Authorization: Bearer <token>`, compared with
  `CryptographicOperations.FixedTimeEquals`; otherwise `401` with `WWW-Authenticate: Bearer`, after the
  same fixed delay as other refused logins on the injected `TimeProvider`. A peer that is not loopback is
  refused `403` even with the token. No answer echoes the token or a password. Bodies are bounded at
  16 MiB (`413`).
- **Requests**, all under `/v1/`:

  | Request | Body | Answer |
  | --- | --- | --- |
  | `GET /v1/events` | query `after`, `kind`, `scheme`, `listen_url`, `account`, `limit` (1 to 1000, default 100) | `{"events": [...], "has_more", "dropped_event_count"}` |
  | `GET /v1/events/wait` | the same query and `timeout_ms` (1 to 60000, default 10000) | `{"event": {...}}`, or `{"timed_out": true}` |
  | `GET /v1/exchanges` | query `after_exchange_id`, `scheme`, `listen_url`, `limit` (1 to 100, default 20) | ADR-0103's `get_exchanges` shape, with `listen_url` and `scheme` |
  | `POST /v1/faults` | `text/plain`: one `--fault` item | `201 {"fault_id": "f3"}` |
  | `GET /v1/faults` | | `{"faults": [{"fault_id", "item", "remaining"}]}` |
  | `DELETE /v1/faults/{id}` | | `204` |
  | `POST /v1/mail?to=<address>` | `message/rfc822`: the message | `204` |
  | `PUT /v1/files/{path}` | the file's bytes | `204` |
  | `DELETE /v1/files/{path}` | | `204` |
  | `POST /v1/messages` | JSON `topic` or `queue`, `payload_text` or `payload_base64`, `retain` | `204` |
  | `POST /v1/accounts` | JSON `user`, `password` | `204` |
  | `DELETE /v1/accounts/{user}` | | `204` |
  | `POST /v1/reset` | | `204` |
  | `POST /v1/snapshots` | | `201 {"snapshot_id": "p1"}` |
  | `POST /v1/snapshots/{id}/restore` | | `204` |

  Events are written as `{"sequence", "at", "kind", ...}` with the record's members in snake_case; bytes
  follow ADR-0103 decision 3's text-or-base64 rule, at most 65536 bytes an event. The endpoint keeps the
  last 10000 events (and at most 64 MiB of bytes) on its own subscription, and 4 snapshots, the oldest
  dropped.
- **Errors** are JSON `{"error": "<code>", "message": "<text>"}`: `400 bad_request` (a malformed query,
  JSON or fault item, with the reader's refusal text), `401 unauthorized`, `403 forbidden`,
  `404 not_found` (an unknown path, fault, snapshot or account), `405 method_not_allowed`,
  `409 conflict` (a disk store asked to reset, snapshot or restore; a snapshot over its bound),
  `413 too_large`, `415 unsupported_media_type`.
- **Changes are published** on the event stream like any other store change or fault application.

### 8. `Surl.Mcp`'s new tools (amends ADR-0103 decision 3)

- **`get_exchanges`** (BL-809): each exchange gains `listen_url` and `scheme`; the schema gains optional
  `scheme` and `listen_url` filters. The bullet "No listen URL per exchange" no longer holds.
- **New tools**, after `get_exchanges` in `tools/list` order, each taking `server_id` (required) with
  `additionalProperties: false`, results as ADR-0103 decision 3's rules (`structuredContent` and the same
  object as the text block; an unknown server ID `isError: true`):

  | Tool | Other properties | Result | Task |
  | --- | --- | --- | --- |
  | `get_events` | `after_sequence`, `kinds` (array), `scheme`, `listen_url`, `account`, `max_events` (1 to 500, default 100) | `{"events", "has_more", "dropped_event_count"}` | BL-809 |
  | `wait_for_event` | the same filters, `timeout_ms` (1 to 60000, default 10000) | `{"event"}` or `{"timed_out": true}` (not an error) | BL-809 |
  | `seed_mail` | `to`, `message` or `message_base64` | `{"delivered": true}` | BL-812 |
  | `put_file` | `path`, `text` or `base64` | `{"path", "byte_count"}` | BL-812 |
  | `publish_message` | `topic` or `queue`, `payload` or `payload_base64`, `retain` | `{"published": true}` | BL-812 |
  | `add_account` | `user`, `password` | `{"user"}` - never the password | BL-812 |
  | `reset_server` | | `{"server_id"}` | BL-812 |
  | `snapshot_server` | | `{"snapshot_id"}` | BL-812 |
  | `restore_server` | `snapshot_id` | `{"server_id", "snapshot_id"}` | BL-812 |
  | `add_fault` | `fault`: one `--fault` item | `{"fault_id"}` | BL-812 |
  | `list_faults` | | `{"faults": [{"fault_id", "item", "remaining"}]}` | BL-812 |
  | `remove_fault` | `fault_id` | `{"fault_id"}` | BL-812 |

- **Bounds.** Seeded content at most 16 MiB decoded; events shown as on the control endpoint; at most 4
  snapshots per server, the oldest dropped, referred to by ID (`p1`, `p2`, ...) and never returned whole.
  A disk-store refusal or a bad fault item is `isError: true` with its text. ADR-0103 decision 4's safety
  rules apply unchanged: no local path is read, and a password is never echoed in a result or log line.

### 9. The extended servers

Delay and drop (the engine), refuse-login (the shared policy), out-of-space (the shared stores) and the
event stream reach every extended server with no change to it. The chosen-error fault needs each server:
BL-793 to BL-798 wire it into the original families only. Each extended server family already built
gets one follow-up task that wires it and adds its schemes' reply shapes to `Surl.Cli`'s check (listed
under Consequences); until it lands, an `error` rule naming one of its schemes is refused as a scheme with
no error shape, so a rule never silently does nothing. **Every protocol server filed after this ADR wires
`error` in its own task** and adds its row to decision 4's reply tables. Servers filed before this ADR
and not yet built (STOMP, XMPP, gRPC, OCI, WebTransport, Socket.IO, SOCKS and the rest) are caught by one
audit task that runs after the follow-ups and files a task per family still without the fault.

## Alternatives considered

- **A central recorder per host** - `Surl.Api`, `Surl.Mcp` and the control endpoint each wiring their own
  `IExchangeLogFactory` and store hooks. Rejected: the "observable" principle says one stream per instance
  that everything subscribes to; per-host recorders would see different things and each new subscriber
  would need its own wiring into every store.
- **A `Surl.Control` that references protocol servers or `Surl.Composition`.** Rejected for isolation:
  ADR-0002 keeps horizontal libraries free of protocol servers, and a control library that knows servers
  could not be unit tested without them. It defines `IControlTarget` and the composition implements it.
- **Unbounded subscriptions, or ones that block the publisher.** Rejected: a slow test subscriber would
  slow or hang the server under test. Drop-oldest with a count keeps servers fast and the loss visible.
- **Free-text chosen errors.** Rejected: see decision 4 - a fault must stay an answer the protocol allows.
- **A generated control token printed at start.** Rejected: secure by default never prints a secret.
- **Seeding every random choice.** Rejected: a seeded key, nonce or token is a known secret; only choices
  that grant nothing are seeded.
- **Replaceable stores.** Rejected for now: see decision 2.
- **Reset of a `--directory` store.** Rejected: a test fixture must never empty a real directory.

## Consequences

- **Contracts first.** BL-823 lands decision 1's and decision 4's contracts and `HookFailedEvent`; the
  library tasks fan out from it: BL-787 (stream, engine), BL-792 (fault plan, delay, drop), BL-788 to
  BL-791 (stores and login), BL-793 to BL-798 (chosen errors), BL-799 to BL-802 (store control).
- **Composition:** BL-803 (one stream and plan per instance), BL-804 (registration list, extended servers
  on it, `AdditionalProtocolServers`), BL-805 (store control), BL-816 (control endpoint live).
- **Configuration:** BL-785 (`--fault`), BL-815 (`--control`, `--control-token-file`, topic `control`);
  `--random-seed` is filed by BL-786.
- **Hosts:** `Surl.Api` BL-806 to BL-808, BL-810, BL-811, BL-813; `Surl.Mcp` BL-809, BL-812;
  `Surl.Control` BL-814. Recipes and the wiki: BL-817 to BL-821.
- **Amended waiting tasks** (this ADR's run): BL-823 (`IsPublishingBytes`, `FindAsync`,
  `HookFailedEvent`, stamping), BL-792 (`FindAsync`), BL-804 (extended servers on the same record, the
  `AdditionalProtocolServers` input), BL-811 (async override, time limit), BL-813 (custom listen URLs
  read by `Surl.Api`), BL-785 (reply shapes for the original schemes only).
- **Extended-server follow-ups** for decision 9: BL-833 (S3, Azure Storage, Google Cloud Storage), BL-834
  (AMQP 0-9-1, AMQP 1.0, Kafka, MQTT 5, NATS), BL-835 (CoAP, MCP, SignalR), BL-836 (DNS, Redis), and the
  audit BL-837 for servers built later.
- `Documentation/Product/Requirements.md` gains FR-060 to FR-065; the glossary defines stand-in, event
  stream, fault rule, snapshot and control endpoint.
