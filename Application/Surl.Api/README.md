# Surl.Api

Starts and stops a Surl server in-process, for a .NET test suite, with no `surl` process.
Surl is the server-side mate of curl: for each protocol upstream curl speaks, it is the server
that answers, so a test can run against it in place of the real web, mail or file server.

The `Surly.Surl.Api` NuGet package is one package with nothing else to restore: every Surl assembly
it needs is inside it.

```csharp
using Surl.Api;
using Surl.Protocol.Abstractions;   // SurlExitCode, SurlEvent, StoreChangedEvent, StoreKind

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello"u8.ToArray();

var server = await SurlServer.StartAsync(options);
// server.BoundUrl is http://127.0.0.1:<the bound port>/ - port 0 takes a free one;
// server.BoundUrls lists every listen URL, in order, with its bound port.
// ... run the client against server.BoundUrl ...
var exitCode = await server.StopAsync();   // SurlExitCode.Ok
```

`await using var server = ...` stops it as well, discarding the exit code.

`mcp+stdio:` is the one listen URL `surl` accepts that `Surl.Api` refuses: it serves the standard
input and output of a `surl` process, which an in-process host has no session on. `StartAsync`
throws `SurlServerStartException` with `SurlExitCode.FailedInit` before any listener binds (with
the extended servers off, `UnsupportedProtocol`, as `surl` does). Listen on `mcp://127.0.0.1:0/` instead.

- Every option is `surl`'s: the properties are spellings of `surl` options (`DataDirectory` is
  `--directory`, `AllowUploads` is `--allow-uploads`, `ListDirectories` is
  `--list-directories`, `FollowSymlinks` is `--follow-symlinks`, `ServeDotFiles` is
  `--serve-dot-files`, `LogLevel` is `--log-level`), and `AdditionalArguments` takes any other
  option in `surl`'s own words. `surl --manual` describes them all.

  | Property | `surl` option |
  | --- | --- |
  | `Accounts` (`SurlAccount(UserName, Password)`, in order) | `--user name:password`, once each |
  | `UserFile` | `--user-file` |
  | `AuthenticationMethods` (empty: `surl`'s default set) | `--auth`, the words joined by commas |
  | `AllowAnonymous`, `AllowPlaintextAuthentication` | `--allow-anonymous`, `--allow-plaintext-auth` |
  | `CertificateFile`, `KeyFile`, `KeyPassphrase`, `CaCertificateFile` | `--cert`, `--key`, `--pass`, `--cacert` |
  | `CertificateType`, `KeyType` (`PEM`, `DER`, `P12` for the certificate; null: PEM) | `--cert-type`, `--key-type` |
  | `LowestTlsVersion`, `HighestTlsVersion` (`SslProtocols.Tls` to `Tls13`; null: TLS 1.2 and 1.3) | `--tlsv1.0` to `--tlsv1.3`, `--tls-max` |
  | `MaxRequestHeadBytes` (at least 1; null: 100 KiB) | `--max-request-head` |
  | `SelfSigned` | `--self-signed` |
  | `HostKeyFiles` (in order), `ThrowawayHostKey` | `--hostkey`, once each; `--throwaway-hostkey` |
  | `MaxConnections`, `MaxConnectionsPerAddress` | `--max-connections`, `--max-connections-per-address` |
  | `IdleTimeout`, `MaxTime` (`TimeSpan.Zero` or `Timeout.InfiniteTimeSpan`: no limit) | `--idle-timeout`, `--max-time`, in seconds |
  | `MaxFileSize` | `--max-filesize`, in bytes |
  | `RandomSeed` | `--random-seed`, 0 to `long.MaxValue`; same seed, same non-secret identifiers |
  | `Faults` (`SurlFault`, in order) | `--fault <item>`, once each |
  | `ControlListenUrl`, `ControlTokenFile` | `--control`, `--control-token-file` |
  | `Cookies` (in order, at most 64) | `--set-cookie <item>`, once each |

- A start is read as `surl` reads a command line, left to right, in this order: `ListenUrls`, then
  each property that differs from its default, then `ConfigFiles` (`--config`), then
  `AdditionalArguments`. A single-valued option given later wins, so a `max-filesize` line in a config
  file beats the `MaxFileSize` property, and `AdditionalArguments` beats both. A repeatable option
  adds up instead (`--user`, `--fault`, `--config` and the other once-each item options), so accounts
  and faults from the properties, the files and `AdditionalArguments` all apply:

  ```csharp
  var options = new SurlServerOptions
  {
      ListenUrls = { "http://127.0.0.1:0" },
      MaxFileSize = 1_000,                        // read first
      ConfigFiles = { "shared.conf" },            // holds "max-filesize = 2000": wins over the property
      AdditionalArguments = { "--max-filesize", "3000" },   // read last: the limit is 3000
  };
  ```

- `AuthenticationMethods` is never null, so a collection initializer works
  (`AuthenticationMethods = { "basic" }`); assigning null leaves it empty.
- `ListenUrls` may be empty when `ConfigFiles` or `AdditionalArguments` is given, since they may name
  listen URLs as `surl` reads them, so a server can start from config files alone:

  ```csharp
  var options = new SurlServerOptions { ConfigFiles = { "surl.conf" } };   // surl.conf holds the listen URLs
  ```

  With all three empty, nothing names a listen URL and the start is an `ArgumentException`. If the
  files and arguments then name none either, `surl`'s own refusal is a `SurlServerStartException`.
- With `SelfSigned`, `SurlServer.SelfSignedCertificatePem` is the public certificate the server made (PEM, never
  its key; `null` otherwise), so a test pins it and keeps verification on. Each server has its own:

  ```csharp
  var pinned = X509Certificate2.CreateFromPem(server.SelfSignedCertificatePem!);
  // trust only `pinned`: X509Chain with CustomRootTrust and pinned in CustomTrustStore
  ```

- Nothing is looser than `surl`: with only a listen URL and an account, anonymous logins,
  plaintext authentication and uploads are all off. Each loosening is a property named for its
  option, and each writes `surl`'s start-up warning to `Log`.
- A start `surl` would refuse throws `SurlServerStartException`, with the `SurlExitCode` and the
  lines `surl` writes after `surl: `; nothing is left bound or locked. Each `Lines` item starts with
  the exit code in parentheses, e.g. `(58) https://127.0.0.1:0/ needs a certificate ...`.
  `SurlExitCode` is in the `Surl.Protocol.Abstractions` namespace.
- `StopAsync` or disposing the server stops it. `StopAsync` returns the `SurlExitCode` `surl`
  would have exited with: `Ok`, or `InternalError` when serving failed; stopping again returns
  the same code. It waits up to 5 seconds (`ServingEngine.DefaultShutdownGracePeriod`) for open
  exchanges to finish, then each connection may linger up to 2 more seconds closing
  (`StreamConnection.LingeringCloseTime`), so a client still holding a connection open makes it take
  about 7 seconds. A test that disposes its clients first stops in milliseconds.
- Misuse is an `ArgumentException`: an option that belongs to a host (`--help`, `--aihelp`,
  `--manual`, `--version`, `--log-file`, `--trace`, `--trace-ascii`) in
  `AdditionalArguments`, an account `--user` could not spell, a negative duration other than
  `Timeout.InfiniteTimeSpan`, or a non-positive recording bound.

## Cookies

Each `Cookies` item is a `--set-cookie` value; over HTTP/1.x, HTTP/2 and HTTP/3 the server writes it as a
`Set-Cookie` header on every reply it matches (ADR-0189). No cookie is set unless one is given, here or
as `--set-cookie` in `ConfigFiles` or `AdditionalArguments`; the limit of 64 counts all three. With
`RecordExchanges` on, a test waits for the client to send it back:

```csharp
options.Cookies.Add("session=t1;Path=/;HttpOnly");
options.RecordExchanges = true;
await using var server = await SurlServer.StartAsync(options);
// ... run the client twice, keeping its cookies ...
var sentBack = (CookieReceivedEvent)await server.WaitForEventAsync(
    new SurlEventQuery { Kind = SurlEventKind.CookieReceived, Name = "session" },
    TimeSpan.FromSeconds(5));
// sentBack.Value is "t1"
```

## What a client sent

Set `RecordExchanges` and read `server.Exchanges` to see each exchange's bytes, oldest first:

```csharp
options.RecordExchanges = true;
await using var server = await SurlServer.StartAsync(options);
// ... run the client ...
var request = server.Exchanges[0].Received;   // every byte the client sent
var response = server.Exchanges[0].Sent;      // every byte the server answered
```

Each `SurlExchangeRecord` has the exchange ID, the `ListenUrl` that accepted it with its bound
port (as `BoundUrls` lists it) and its `Scheme`, so a server on several listen URLs tells them
apart, the client's endpoint, `StartedAt` (from `TimeProvider`), the `Chunks` in the order the
bytes crossed (one per read or write, one per datagram), and `IsTruncated`. At most
`MaxRecordedExchanges` (1000) are kept, the oldest dropped and counted in `DroppedExchangeCount`;
at most `MaxRecordedBytesPerExchange` (1 MiB, both directions together) of each, the rest cut.
The record is a subscriber of the server's event stream: should the stream ever drop events for
it, every exchange still open is marked `IsTruncated` rather than left silently short. Reading
`Exchanges` first waits for every event already published, so a client that has finished finds
its exchange complete. Recording is off by default and costs nothing then; reading `Exchanges`
with it off throws `InvalidOperationException`.

## Finding and waiting for what happened

With `RecordExchanges` on, ask for the exchanges and events a test cares about with a
`SurlEventQuery`, and await the first that matches instead of polling (ADR-0143):

```csharp
// The mail for bob delivered over SMTP within 5 seconds.
var delivered = await server.WaitForEventAsync(
    new SurlEventQuery { Scheme = "smtp", Kind = SurlEventKind.StoreChanged, Store = StoreKind.Mail, Account = "bob" },
    TimeSpan.FromSeconds(5));

// WaitForEventAsync returns the base SurlEvent (in Surl.Protocol.Abstractions, as are
// StoreChangedEvent and StoreKind); a StoreChangedEvent's path, mailbox, topic, queue, key or account is its Name.
var path = delivered is StoreChangedEvent changed ? changed.Name : null;   // or ((StoreChangedEvent)delivered).Name

var ftpLogins = server.FindExchanges(new SurlEventQuery { Scheme = "ftp", Account = "alice" });
var faults = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied });
```

Every property left unset matches anything: `Scheme`, `ListenUrl` (as `BoundUrls` lists it),
`Account` (an exchange in which that account's login was judged, or an event naming it), `Kind`,
`Store`, `Name` (a `StoreChanged` event's path, mailbox, topic, queue, key or account, matched exactly or as
a segment prefix, so `/uploads` matches `/uploads/a.txt` but not `/uploads2`, ADR-0164 decision 8; or a
`CookieReceived` event's cookie name, matched exactly, ADR-0189 decision 5; case-sensitive; no other event
matches it, and an exchange matches it only by its store changes), `Since` (inclusive) and `Until` (exclusive) on `TimeProvider`'s clock, and
`AfterSequence`. An exchange matches `Kind` and `Store` when one of its events does. Events are the
server's own event records without their bytes, at most `MaxRecordedEvents` (10000) of them, the
oldest dropped and counted in `DroppedEventCount`. A wait checks what is already kept first, so it
never misses an event that came before it; its timeout runs on `TimeProvider`, so a fake clock
controls it, and a wait that times out throws `TimeoutException` naming the query and how many
events arrived meanwhile. A cancelled wait throws `OperationCanceledException`. With recording off,
every query throws `InvalidOperationException` as `Exchanges` does.

## Making the server misbehave

A test makes one listener, account or path misbehave on purpose with a `SurlFault` rule
(ADR-0124 decision 4): `Delay`, `DropConnection`, `RefuseLogin`, `ProtocolError` (a reply in the
scheme's error shape) or `OutOfSpace`, narrowed with `Listener`, `Scheme`, `Account`, `Path`,
`Command`, `Stage`, `AfterBytes`, `Close` and `Times`. `Close` (`FaultClose`, default `Reset`) says how a
drop ends the connection: `Reset` aborts it, `Graceful` closes it so the peer sees an ordinary end of
stream (ADR-0188 decision 4); it is written after `after-bytes` only when it is not `Reset`. Over HTTP/3
(`--http3`), `Reset` only stops answering and `Graceful` closes the QUIC connection with `CONNECTION_CLOSE`
`NO_ERROR`, which pinned upstream curl reports at once as exit 56 (ADR-0188 amendment 5); every other datagram
protocol only stops answering. Give rules at start in `Faults`, or change them while
the server runs:

```csharp
var id = server.Faults.Add(SurlFault.DropConnection() with { Listener = "smtp://127.0.0.1:0" });   // "f1"
// ... the next SMTP connection is dropped before any byte ...
server.Faults.Remove(id);   // normal serving again; Clear() removes every rule
```

Each rule is one `surl --fault` item read by `surl`'s own reader: a start rule `surl` would refuse
throws `SurlServerStartException`, a runtime one `ArgumentException`, each with `surl`'s text
(`option --fault: kind error needs key scheme or key listener`). Rules given at start write `surl`'s
`--fault` warning to `Log`; a rule added at run time writes nothing. Each rule applied is a
`FaultApplied` event the queries above find.

`List()` reads the plan as it stands, in the order its rules are tried: each `SurlFaultRule`'s
`Id`, its `Item` - the `--fault` item without `times`, which `surl` reads back as the same rule - and
`Remaining`, what is left of its `Times` (0 once spent, `null` for a rule that always applies).
Response overrides are not listed.

```csharp
server.Faults.Add(SurlFault.DropConnection() with { Times = 2 });
var rule = server.Faults.List()[0];   // ("f1", "drop", 2); after one dropped connection, Remaining is 1
```

## Control endpoint from a .NET harness

A harness that also drives the server from outside .NET (a script, another process) serves `surl`'s
loopback control endpoint from the same instance (ADR-0124 decision 7, BL-1176):

```csharp
options.ControlListenUrl = "http://127.0.0.1:0";   // --control: http, loopback only, port 0 allowed
options.ControlTokenFile = "control.token";        // --control-token-file: the bearer token's file
await using var server = await SurlServer.StartAsync(options);
var control = server.ControlUrl;                    // http://127.0.0.1:<bound port>/, null without the options
```

Both are composed as `surl`'s own options, so every refusal is `surl`'s, thrown as a
`SurlServerStartException` with `FailedInit` (2): a non-loopback URL, a scheme other than `http`,
`ControlListenUrl` without `ControlTokenFile` (no token is generated) and `ControlTokenFile` without
`ControlListenUrl`; an unreadable or malformed token file is refused as `surl` refuses it. The start-up
warning goes to `Log`. Tests: `SurlServerTests.Control`
(`StartAsync_ControlListenUrlAndATokenFile_ReportsTheBoundControlUrlAndAnswersEvents`,
`StartAsync_WithoutControlOptions_ControlUrlIsNull`,
`StartAsync_ARefusedControlCombination_ThrowsWithSurlsMessage`).

## Hooks

Hooks are test code the server runs (ADR-0124 decision 5). They are `Surl.Api` only: the control
endpoint and `Surl.Mcp` offer fault rules, never callbacks.

An **event callback** runs for each event a `SurlEventQuery` matches, from the moment it is
registered until its registration is disposed or the server stops, whether or not
`RecordExchanges` is on:

```csharp
using var registration = server.OnEvent(
    new SurlEventQuery { Scheme = "smtp", Kind = SurlEventKind.StoreChanged },
    (surlEvent, cancellationToken) => { delivered.Add(surlEvent); return ValueTask.CompletedTask; });
```

A **response override** is a fault rule whose answer is computed per request. At each point its
`SurlFaultScope` matches (`Listener` or `Scheme`, `Account`, `Path`, `Command`, `Stage`; null matches
anything), it returns the `SurlFault` to apply there, or null for none:

```csharp
var id = server.Faults.AddOverride(
    new SurlFaultScope { Scheme = "http", Stage = FaultStage.Command },
    (point, cancellationToken) => ValueTask.FromResult(
        point.Path == "/a" ? SurlFault.ProtocolError("503 Busy") : null));   // "f1"; Remove(id) takes it out
```

The bounds - nothing a hook does can loosen a secure default, hang the server or crash it:

- **A callback never blocks a server.** Each runs on its own subscription of the event stream
  (65536 events) on its own task, one event at a time; a callback slower than the events loses the
  oldest, as any slow subscriber does. Exchange bytes never reach it. Disposing the registration
  cancels the token it was given and runs it no more.
- **An override is bounded by `HookTimeLimit`** (`SurlServerOptions`, 1 second by default, 1 ms to
  30 seconds) on the server's `TimeProvider`, and runs off the serving thread. Past the limit its
  token is cancelled, the request is served as if no override were there, and a `HookFailedEvent`
  with `TimedOut` is published.
- **A hook that throws is caught.** A `HookFailedEvent` with `Threw` and the exception's message is
  published, naming the override's rule ID (`f1`) or the callback's (`h1`, `h2` ...); the request is
  served as if no override were there, or the callback goes on to the next event. The hook stays
  registered. A callback that throws on its own `HookFailedEvent` is not published again.
- **An override can choose only what a fault rule can.** Its answer is read by `surl`'s own
  `--fault` reader and applies to the point it was asked at: its own scope and `Times` are ignored,
  and an error's reply shape is the point's scheme's. An answer `surl` would refuse, or one whose
  kind cannot act at the point's stage (a `RefuseLogin` at a request), is published as `Threw` and
  ignored. So an override cannot accept a refused login, turn TLS off, reach outside the served root
  (a `.surl` path stays unserved) or widen an option.
- An override sits in the fault plan's order like any rule: a rule added before it wins first, and
  an answer of null goes on to the next rule. Overrides count toward the 256 rules and are not
  listed among the plan's rules.

## Seed, reset, snapshot

One server can serve a whole test class without one test leaking into the next (ADR-0124
decision 3). Seed what a test needs through the path a client would write by, so names, size
limits and events apply as they would to a client; a seed never trips a fault rule:

```csharp
await server.Mail.DeliverAsync("alice@example.com", "Subject: hi\r\n\r\nhello\r\n"u8.ToArray());
await server.Files.PutAsync("/reports/a.txt", "contents"u8.ToArray());
await server.Files.DeleteAsync("/reports/a.txt");              // a test deletes it again; no file there throws Absent
                                                                // (/reports stays, empty, as after a client's delete;
                                                                // ResetAsync returns the store to its configured state)
await server.Messages.PublishAsync("sensors/1", "21.5"u8.ToArray(), retain: true);
await server.Messages.EnqueueAsync("jobs", "job 1"u8.ToArray());   // the queue must already exist
server.Accounts.Add("bob", "builder");                              // Remove("bob") takes it away
```

`Accounts.Remove` of the last account does not open the server: without `--allow-anonymous` (`AllowAnonymous`) it
still asks for a login wherever it did, so on a scheme that has had an account to log in to, an `http` `GET` without
credentials gets `401` or `403` (ADR-0132 amendment 5). The removal stays allowed, `Accounts.HasAccounts` turns
`false`, which means no account is held, and
`surl: warning: no local account left; the server still asks for a login wherever it did` goes to `Log`;
`Accounts.Add` lets that account in. `ResetAsync` returns what the configured accounts give, so a server started with
no account serves anonymous reads again, and `RestoreAsync` returns what held when the snapshot was taken. When the
server was started with `--user-ldap`, the directory is an account source (ADR-0149 amendment 1) and the line is the
same: on the schemes the directory serves, a password login naming a user no account holds, the removed one included,
is checked against it.

`ProxyAccounts.Remove` of the last proxy account does not open the proxy: without `--allow-anonymous`
(`AllowAnonymous`) it then lets no new client through, so a `GET` without credentials gets the `407` (ADR-0132
amendment 4). The removal stays allowed, `ProxyAccounts.HasAccounts` turns `false` and
`surl: warning: no proxy account left; without --allow-anonymous the proxy now lets no new client through` goes to
`Log`; `ProxyAccounts.Add` lets that account in. A tunnel or relay already set up stays up.

A retained seed (`Messages.PublishAsync(topic, payload, retain: true)`, `surl-mcp`'s `publish_message` and the
control endpoint alike) is kept on any topic, including one outside every `--topic ...;retain` filter: the filter
only decides what a client publish retains (a curl client cannot set RETAIN 1), and a seed is the test's own setup
(ADR-0176 amendment 1).

`Mail.DeliverAsync` finds its recipient as SMTP's `RCPT TO` does. Under `AllowAnonymous` every valid
recipient goes to the one anonymous mailbox set. Otherwise, with no `MailDomains`, only the local part
counts and the domain is ignored: an account is matched exactly, or else as the one account equal
ignoring case, so `ALICE@example.com` and `alice@other.org` both reach account `alice`. With a mail
domain, the routing of ADR-0102 decision 1.1 applies.

The Redis keyspace, the LDAP directory and the DNS zones have their own members (ADR-0158):

```csharp
await server.Keys.SetAsync("seeded", "yes", TimeSpan.FromHours(1), database: 0);   // or SetAsync(byte[] key, byte[] value, ...)
await server.Directory.AddEntriesAsync("dn: dc=example,dc=com\nobjectClass: domain\ndc: example\n");
await server.Zones.AddZoneAsync("example.com", zoneFileText);
```

`Keys.SetAsync` sets a string key (key and value together at most 16 MiB, database 0 to 15, expiry 1
millisecond to 365 days, else `ArgumentOutOfRangeException`; an empty key is an `ArgumentException`).
`Directory.AddEntriesAsync` adds LDIF content records, all or none, at most 16 MiB; a `changetype:` record
and a value read from a file or URL are refused. `Zones.AddZoneAsync` adds one zone, at most 16 MiB; a second
zone for a held apex is refused until a reset. Each throws `SurlSeedRefusedException` (`Store` is
`Keyspace`, `Directory` or `Zones`) when refused or when no Redis, LDAP or DNS server is composed.

A seed a client would be refused throws `SurlSeedRefusedException`, never fails silently: its
`Store` is the store and its `Refusal` the store's own word for it - `NoSuchAccount` for mail to an
unknown mailbox, `NotPermitted` for a `.surl` path, `TooLarge` or `MessageTooLarge` past
`MaxFileSize`, `InvalidTopic`, `UnknownQueue`, `StoreFull`. `Accounts.Add` throws
`ArgumentException` for a user name with `:` or one that exists.

```csharp
var clean = await server.SnapshotAsync();   // after the class's own seeding
// ... a test changes things ...
await server.RestoreAsync(clean);           // back to the snapshot
await server.ResetAsync();                  // or back to how the server started
```

`ResetAsync`, `SnapshotAsync` and `RestoreAsync` take every store the server holds, in this order: files,
mail, messages and accounts, then, each only when composed or served, proxy accounts, TLS-SRP accounts, the
Redis keyspace, the LDAP directory, AMQP 0-9-1 exchanges, Kafka producers, sessions, DNS zones and uploads
(`StoreKind` names each; ADR-0158, ADR-0159, ADR-0167, ADR-0170).

| State | `ResetAsync` leaves | A snapshot copies | `RestoreAsync` leaves |
| --- | --- | --- | --- |
| Files | the `Files` given at start and no others | yes | the snapshot's |
| Mail | the configured mailboxes holding nothing | yes, with UIDs and flags | the snapshot's |
| Messages | no retained or queued messages | retained messages, topic logs, committed offsets and queues, except a queue exclusive to a connection | the snapshot's |
| Accounts, proxy accounts, TLS-SRP accounts | exactly those given at start | yes | the snapshot's |
| Redis keyspace, LDAP directory, DNS zones | empty databases, an empty directory, no zone | yes | the snapshot's |
| AMQP 0-9-1 exchanges and bindings | none a client declared | yes | the snapshot's |
| Kafka producer state | none | yes | the snapshot's |
| Kafka consumer groups | dropped | no | dropped |
| MQTT 5 sessions kept with no connection | none | yes, without queued deliveries | the snapshot's, except where a client connected under the same identifier keeps its own |
| MQTT 5 pending Wills, queued deliveries and unacknowledged publishes | discarded | no | discarded |
| OCI registry tokens | revoked | no | revoked |
| MCP OAuth clients, codes and tokens, and MCP HTTP sessions | deleted and ended | no | deleted and ended |
| Uploads open or staged (S3, Azure Storage, Google Cloud Storage, OCI), and Azure Storage's remembered committed block lists | discarded | no | discarded, even one begun before the snapshot |

A snapshot is an in-memory copy (at most 256 MiB) that only the server that took it restores; another
server's throws `ArgumentException`. Every mailbox gets a new `UIDVALIDITY` on reset and restore,
so an IMAP client resynchronises; open connections stay open and see the new contents on their
next command. Each seed publishes `StoreChanged` events the queries above find, and each store publishes one
on reset and on restore, except `StoreKind.Uploads` and `StoreKind.Sessions`, under which each server
publishes its own: uploads named `s3`, `azure`, `gcs` and `oci`; sessions with an empty name (MQTT 5),
`oci` and `mcp`. With `DataDirectory` the files, mail and messages are on disk, and reset and snapshot throw
`InvalidOperationException` at the files, the first store, so a fixture never empties a real directory; a
share on disk makes them throw there too. Seeding files, mail and messages on disk still works.

**A snapshot holds secrets.** Each account and proxy account is held with the SHA-256 of its
password and, when it is named, the credentials derived from the password, some of which are the
password's bytes (its UTF-8 bytes, and the bytes a base64 password decodes to), so treat a
snapshot as holding every named account's password. The empty-name Bearer-token account is held
as the SHA-256 of its token only. A TLS-SRP account is held as its user name, salt, group and
verifier, never its password (ADR-0132 amendment 3, ADR-0170).

## Read back what a client left

Reads need no client of the test's own (ADR-0164). Each member is async, takes a `CancellationToken`
last, and returns a copy; they throw `ObjectDisposedException` once the server is stopped.

| Member | Returns |
| --- | --- |
| `Mail.ListMailboxesAsync(owner)` | `IReadOnlyList<SurlMailboxInfo>` (`Name`, `MessageCount`, `UnseenCount`, `UidValidity`, `Use`) |
| `Mail.ListMessagesAsync(owner, mailbox, afterUid = 0, limit = 100)` | `SurlMessagePage` (`Messages` of `Uid`, `Size`, `Flags`, `InternalDate`; `HasMore`) |
| `Mail.ReadMessageAsync(owner, mailbox, uid)` | `SurlMailMessage` (`Summary`, `Bytes`) |
| `Files.ReadAsync(path)` | `SurlFileContents` (`Bytes`, `ByteCount`, `ModifiedAt`, `ContentType`) |
| `Files.ListAsync(path, afterName = null, limit = 100)` | `SurlFolderPage` (`Entries` of `Name`, `IsFolder`, `ByteCount`; `HasMore`) |
| `Messages.BrowseQueueAsync(queue, afterPosition = -1, limit = 100)` | `SurlQueuePage` (`Messages`, `ReadyCount`, `InFlightCount`, `HasMore`) |
| `Messages.ReadRetainedAsync(topic)` | `SurlRetainedMessage?`, `null` when none |
| `Messages.ReadLogAsync(topic, fromOffset = 0, limit = 100)` | `SurlLogPage` (`Records`, `NextOffset`, `HasMore`) |
| `Keys.GetAsync(key, database = 0)` | `SurlKeyContents` (`Info` of `Key`, `KeyBytes`, `Type`, `ByteCount`, `ExpiresAt`; `Bytes`, `Text`; no value for a type other than `string`) |
| `Keys.ListAsync(pattern, afterKey = null, limit = 100, database = 0)` | `SurlKeyPage` (`Keys` as `SurlKeyInfo`, never a value; `HasMore`), a `KEYS` glob, ordinal order |
| `Directory.ExportLdifAsync()` | the directory as LDIF text (`version: 1`, then each entry), every `userPassword` value omitted; nothing published |
| `Zones.ListAsync()` | the apexes of the DNS zones held, each with its final dot, in the order they were added; nothing published |
| `Zones.ReadAsync(apex)` | one zone as master-file text (a record a line, absolute names, TTL, class `IN`); `SurlNotFoundException` for an apex no zone has; nothing published |

An absent owner, mailbox, UID, file, folder, queue, log or key throws `SurlNotFoundException`; a `limit` outside
1 to 1000, a bad UID, position or offset, an item past 16 MiB, or a path a request may not name throws
`SurlReadRefusedException`. A read never sets a flag, consumes or acknowledges a queued message, publishes an
event or consults a fault rule, and it works with `DataDirectory`. The `.surl` state folder is not served.
Flags are IMAP system flags with their backslash, as RFC 9051 writes them (`\Seen`). `SurlEventQuery.Name`, above, finds the change that put it there.

## Your own protocol server

A test that needs a protocol Surl does not serve, or a deliberately odd one, registers its own
`IConnectionProtocolServer` or `IDatagramProtocolServer` for a new scheme (ADR-0124 decision 2). It
is served beside the built-in servers, through the same listeners, limits, event stream and fault
plan, so its exchanges are recorded and its listen URL can be faulted like any other:

```csharp
var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0", "echo://127.0.0.1:0" } };
options.ProtocolServers.Add(new SurlProtocolServer("echo", services => new EchoServer(services.ContentStore)));
options.Faults.Add(SurlFault.DropConnection() with { Listener = "echo://127.0.0.1:0", Times = 1 });
await using var server = await SurlServer.StartAsync(options);
// server.BoundUrls[1] is echo://127.0.0.1:<port>/
```

- The factory gets `SurlProtocolServices`: the instance's content store, mail store, message bus,
  authentication policy and clock, the same ones the built-in servers share.
- Add only: a scheme any original or extended server claims, turned on or not, or one given twice,
  is an `ArgumentException` at start.
- A custom listen URL names its port (0 for an ephemeral one), `scheme://host:port`, nothing more.
- A fault rule in `Faults` or `server.Faults.Add` may name the custom listener or scheme, except an
  `error` rule, which is refused (`ArgumentException`) unless the server declares `FitsErrorReply`, a
  `Func<string, bool>` that says whether a rule's `reply` is a chosen error the protocol allows (ADR-0172).
  A fitting rule is accepted at start and at `Add` and reaches the server through
  `ExchangeContext.Faults`, which answers `Reply` in its own wire format; a reply that does not fit, or a
  predicate that throws, is an `ArgumentException` naming the rule. An `AddOverride` error answer for the
  custom scheme is judged the same way, and a refused one publishes `HookFailedEvent`.
  `AdditionalArguments` cannot name a custom scheme.

`Surl.Composition`'s public types, which this package carries, are not a supported programming
interface and may change in any release.
