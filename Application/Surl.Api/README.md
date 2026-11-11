# Surl.Api

Starts and stops a Surl server in-process, for a .NET test suite, with no `surl` process.
Surl is the server-side mate of curl: for each protocol upstream curl speaks, it is the server
that answers, so a test can run against it in place of the real web, mail or file server.

The `Surl.Api` NuGet package is one package with nothing else to restore: every Surl assembly
it needs is inside it.

```csharp
using Surl.Api;

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello"u8.ToArray();

var server = await SurlServer.StartAsync(options);
// server.BoundUrl is http://127.0.0.1:<the bound port>/ - port 0 takes a free one;
// server.BoundUrls lists every listen URL, in order, with its bound port.
// ... run the client against server.BoundUrl ...
var exitCode = await server.StopAsync();   // SurlExitCode.Ok
```

`await using var server = ...` stops it as well, discarding the exit code.

- Every option is `surl`'s: the properties are spellings of `surl` options (`DataDirectory` is
  `--directory`, `AllowUploads` is `--allow-uploads`, `ListDirectories` is
  `--list-directories`, `FollowSymlinks` is `--follow-symlinks`, `ServeDotFiles` is
  `--serve-dot-files`, `LogLevel` is `--log-level`), and `AdditionalArguments` takes any other
  option in `surl`'s own words. `surl --manual` describes them all.

  | Property | `surl` option |
  | --- | --- |
  | `Accounts` (`SurlAccount(UserName, Password)`, in order) | `--user name:password`, once each |
  | `UserFile` | `--user-file` |
  | `AuthenticationMethods` (null: `surl`'s default set) | `--auth`, the words joined by commas |
  | `AllowAnonymous`, `AllowPlaintextAuthentication` | `--allow-anonymous`, `--allow-plaintext-auth` |
  | `CertificateFile`, `KeyFile`, `KeyPassphrase`, `CaCertificateFile` | `--cert`, `--key`, `--pass`, `--cacert` |
  | `SelfSigned` | `--self-signed` |
  | `HostKeyFiles` (in order), `ThrowawayHostKey` | `--hostkey`, once each; `--throwaway-hostkey` |
  | `MaxConnections`, `MaxConnectionsPerAddress` | `--max-connections`, `--max-connections-per-address` |
  | `IdleTimeout`, `MaxTime` (`TimeSpan.Zero` or `Timeout.InfiniteTimeSpan`: no limit) | `--idle-timeout`, `--max-time`, in seconds |
  | `MaxFileSize` | `--max-filesize`, in bytes |
  | `Faults` (`SurlFault`, in order) | `--fault <item>`, once each |

- Nothing is looser than `surl`: with only a listen URL and an account, anonymous logins,
  plaintext authentication and uploads are all off. Each loosening is a property named for its
  option, and each writes `surl`'s start-up warning to `Log`.
- A start `surl` would refuse throws `SurlServerStartException`, with the `SurlExitCode` and the
  lines `surl` writes after `surl: `; nothing is left bound or locked.
- `StopAsync` or disposing the server stops it. `StopAsync` returns the `SurlExitCode` `surl`
  would have exited with: `Ok`, or `InternalError` when serving failed; stopping again returns
  the same code.
- Misuse is an `ArgumentException`: an option that belongs to a host (`--help`, `--aihelp`,
  `--manual`, `--version`, `--log-file`, `--trace`, `--trace-ascii`) in
  `AdditionalArguments`, an account `--user` could not spell, a negative duration other than
  `Timeout.InfiniteTimeSpan`, or a non-positive recording bound.

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

var ftpLogins = server.FindExchanges(new SurlEventQuery { Scheme = "ftp", Account = "alice" });
var faults = server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied });
```

Every property left unset matches anything: `Scheme`, `ListenUrl` (as `BoundUrls` lists it),
`Account` (an exchange in which that account's login was judged, or an event naming it), `Kind`,
`Store`, `Since` (inclusive) and `Until` (exclusive) on `TimeProvider`'s clock, and
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
`Command`, `Stage`, `AfterBytes` and `Times`. Give rules at start in `Faults`, or change them while
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
await server.Messages.PublishAsync("sensors/1", "21.5"u8.ToArray(), retain: true);
await server.Messages.EnqueueAsync("jobs", "job 1"u8.ToArray());   // the queue must already exist
server.Accounts.Add("bob", "builder");                              // Remove("bob") takes it away
```

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

`ResetAsync` returns files, mail, messages and accounts, in that order, to the server's start:
the `Files` given at start and no others, the configured mailboxes holding nothing, no retained or
queued messages, and exactly the accounts given at start. A snapshot is an in-memory copy (at most
256 MiB, accounts as password hashes only) that only the server that took it restores; another
server's throws `ArgumentException`. Every mailbox gets a new `UIDVALIDITY` on reset and restore,
so an IMAP client resynchronises; open connections stay open and see the new contents on their
next command. Each seed, reset and restore is a `StoreChanged` event the queries above find. With
`DataDirectory` the files, mail and messages are on disk, and reset and snapshot throw
`InvalidOperationException` so a fixture never empties a real directory; seeding them still works.

`Surl.Composition`'s public types, which this package carries, are not a supported programming
interface and may change in any release.
