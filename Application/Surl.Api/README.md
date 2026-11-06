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

`Surl.Composition`'s public types, which this package carries, are not a supported programming
interface and may change in any release.
