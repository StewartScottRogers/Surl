# Surl.Api

The .NET programming interface that starts and stops Surl's protocol servers in-process for a
test suite (ADR-0098; its design is ADR-0099 decisions 3 to 8). It is a product named for
itself, so it carries no `.UnitLibrary` suffix (a naming exception like `Surl.Console`), and it
is held to the same four quality gates as every `*.UnitLibrary`; `Measure-CodeQuality.ps1`
measures it by name.

## What it is

- `SurlServer.StartAsync(SurlServerOptions, CancellationToken)` turns the options into `surl`'s
  own words (`SurlServerArguments.Compose`: each listen URL, then each property that differs
  from its default as the option it names, then `AdditionalArguments`), parses them with
  `Surl.Cli`'s `CommandLineParser` and starts them through `Surl.Composition`'s
  `SurlServerComposition.ComposeAsync` and `ComposedServing.ServeAsync`, the same start `surl`
  runs. So every default is `surl`'s and every refusal is `surl`'s: it throws
  `SurlServerStartException` with the `SurlExitCode` and the lines `surl` writes after `surl: `.
- It returns once every listener has bound; `BoundUrls` lists each listen URL in order with
  its bound port, as the status line writes it. `StopAsync` and `DisposeAsync` stop serving and
  release the data directory's lock.
- Content is a data directory (`DataDirectory`, `--directory`) or files supplied in memory
  (`Files`, written by `InMemoryFiles` into the `InMemoryContentFileSystem` handed to the
  composition as `ContentWithoutDataDirectory`).
- `Log` receives the start-up warnings, the status lines and the exchange log at `LogLevel`;
  null is silent. `--help`, `--aihelp`, `--manual`, `--version`, `--log-file`, `--trace` and
  `--trace-ascii` belong to a host and are `ArgumentException`s.
- Accounts (`SurlAccount`), `UserFile`, `AuthenticationMethods`, the loosenings, TLS files,
  host keys and limits are typed properties (BL-616), each composed as its option by
  `SurlServerArguments.Compose`; a null or default property adds no word, so the default is
  `surl`'s. Misuse `--user` could not spell (a null, a `:` in a user name) and a negative
  duration other than `Timeout.InfiniteTimeSpan` are `ArgumentException`s.
- `RecordExchanges` (ADR-0099 decision 7 as ADR-0124 decision 1 amends it, off by default and
  adding no word) makes an `ExchangeRecordSubscriber`: it subscribes to `ComposedServing.Events`
  with bytes, at the largest capacity, before serving starts, and a pump task applies every event
  in stream order, under its `Gate`, to an `ExchangeRecordBook`. `SurlServer.Exchanges` snapshots
  the book as `SurlExchangeRecord`s (exchange ID, `ListenUrl` and `Scheme` from the
  `ExchangeStartedEvent`, client, `StartedAt`, the `SurlExchangeChunk`s in order, `Received`,
  `Sent`, `IsTruncated`) once the pump has applied every event the stream had published when the
  read began; `DroppedExchangeCount` counts the oldest dropped past `MaxRecordedExchanges`; bytes
  past `MaxRecordedBytesPerExchange` (both directions together) are cut. When the subscription
  drops events, every exchange not yet ended is marked `IsTruncated`; bytes or an end for an
  exchange whose start was dropped are ignored. Stopping disposes the subscription and waits for
  the pump, so the record stays readable. With recording off both throw
  `InvalidOperationException`; a non-positive bound is an `ArgumentException`. The levelled or
  trace log keeps its own `IExchangeLogFactory`.
- The same subscriber feeds an `EventRecordBook` (every event but bytes, at most
  `MaxRecordedEvents`, `DroppedEventCount`), and each kept exchange carries `ExchangeFacts` (its
  start, accounts judged, event kinds, stores changed). `FindExchanges`, `FindEvents`,
  `WaitForExchangeAsync` and `WaitForEventAsync` take a `SurlEventQuery`, matched by
  `ExchangeFacts.Matches` and `SurlEventFacts.Matches` (ADR-0143). A wait checks the books first,
  then wakes on each applied event (a `TaskCompletionSource` swapped under `Gate`); its timeout is a
  `CancellationTokenSource` on the instance's `TimeProvider`, and it throws `TimeoutException`
  with `ExchangeRecordSubscriber.TimedOutMessage`. Tests drive waits with `ManualTimeProvider`.
- Fault rules (BL-808, ADR-0124 decision 4): `SurlServerOptions.Faults` holds `SurlFault`
  records, each composed by `SurlFault.ToItem` as one `--fault` item (values percent-encoded) after
  the limits. `SurlServer.Faults` is a `SurlServerFaults` over `ComposedServing.Faults`: `Add`
  parses the item beside a fixed listen URL with `CommandLineParser`, so a refusal is the
  `ArgumentException` carrying `surl`'s text, then adds `FaultPlanComposition.ToRule`'s rule and
  returns its ID; `Remove` and `Clear` go straight to the plan. A runtime rule logs nothing.
  Tests bring clients in after start with `FakeListenerFactory.Accept`.
- Store control (BL-810, ADR-0124 decision 3): `SurlServer.Mail`, `Files`, `Messages` and
  `Accounts` (`SurlServerMail`, `SurlServerFiles`, `SurlServerMessages`, `SurlServerAccounts`) are
  thin wrappers over `ComposedServing.Stores` (`ComposedStores`); each store result that is not a
  success becomes a `SurlSeedRefusedException` (`Store`, `Refusal` = the store's enum member name).
  `ResetAsync`, `SnapshotAsync` (a `SurlSnapshot` wrapping `ComposedSnapshot` and its owning
  server) and `RestoreAsync` (another server's snapshot is an `ArgumentException`) go straight to
  `ComposedStores`, whose `ComposedStoreException` is the `InvalidOperationException` a refused
  reset or snapshot throws. No file delete yet: `ComposedStores` has none. Tests bring clients in
  on one listener of several with `FakeListenerFactory.Accept(scheme, connection)`.
- `SurlServerHostSeams.ReadStartFile` is handed to the composition's `ReadStartFile`, so tests
  give the `--user-file` and `--hostkey` files in memory (`ApiTestSeams.StartFiles`). The
  `--cert`, `--key` and `--cacert` files are still read from disk by `ServerTlsComposition`.

## What it references

`Surl.Composition`, `Surl.Cli`, `Surl.Content`, `Surl.Core` (`SurlEventStream`), `Surl.Networking`
(`SocketListenerFactory`), `Surl.Output` and `Surl.Protocol.Abstractions`. It is a composition root: no protocol server or
horizontal library may reference it (ADR-0099 decision 11).

## How it is tested

`Application/Surl.Api.UnitTests`, with no network and no disk: `SurlServerHostSeams` is the
internal seam, and the tests start servers through `ApiTestSeams` - a `FakeListenerFactory`
that binds nothing and reports port 49731, an in-memory data directory, an in-memory lock table
that refuses a second start on a held data directory with 124 as `DataDirectoryLock` does, and
`SURL_EXTENSIONS=0`. `SurlServerHostSeams.Production` is what the public `StartAsync` uses:
socket listeners, `DataDirectoryProbe.CanOpen`, `DataDirectoryLock.Take` and the process's
environment.
