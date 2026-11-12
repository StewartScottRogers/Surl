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
  `List` (BL-963, ADR-0124 decision 8's `list_faults`) snapshots `FaultPlan.Rules` as
  `SurlFaultRule`s: `Id`, `Item` (`SurlFault.WithoutCountFrom(rule).ToItem()`, every key but
  `times`, so a spent rule still spells an item `surl` takes) and `Remaining` (the rule's `Times`,
  what is left of its count, `null` for always). Overrides are not listed.
  Tests bring clients in after start with `FakeListenerFactory.Accept`.
- Store control (BL-810, ADR-0124 decision 3): `SurlServer.Mail`, `Files`, `Messages` and
  `Accounts` (`SurlServerMail`, `SurlServerFiles`, `SurlServerMessages`, `SurlServerAccounts`) are
  thin wrappers over `ComposedServing.Stores` (`ComposedStores`); each store result that is not a
  success becomes a `SurlSeedRefusedException` (`Store`, `Refusal` = the store's enum member name).
  `ResetAsync`, `SnapshotAsync` (a `SurlSnapshot` wrapping `ComposedSnapshot` and its owning
  server) and `RestoreAsync` (another server's snapshot is an `ArgumentException`) go straight to
  `ComposedStores`, whose `ComposedStoreException` is the `InvalidOperationException` a refused
  reset or snapshot throws. `Files.DeleteAsync` (BL-954) is `ComposedStores.DeleteFileAsync` over `ContentStore.DeleteFileForTheTest`: no fault plan, publishes `Deleted`; no file there is `Absent`. Tests bring clients in
  on one listener of several with `FakeListenerFactory.Accept(scheme, connection)`.
- Hooks (BL-811, ADR-0124 decision 5): `SurlServer.OnEvent` makes an `EventCallbackHook`
  (`h1`, `h2` ...): its own `SurlEventStream` subscription at the default capacity without bytes, a
  pump task that keeps `ExchangeFacts` for each exchange open since it subscribed, so
  `SurlEventFacts.Matches` reads a query as `FindEvents` does, and runs the callback one event at a
  time. A throw is published as `HookFailedEvent` (`Threw`) unless the hook was disposed or the event
  was its own failure. Disposing cancels the callback's token; the server disposes every callback
  on stop. `SurlServerFaults.AddOverride` adds a `ResponseOverrideHook` to the plan through
  `FaultPlan.AddOverride` (Surl.Core) under the next `f<n>`: the decision is started with
  `Task.Factory.StartNew` (so a thrown `OperationCanceledException` stays a failure) and raced
  against `Task.Delay(HookTimeLimit, TimeProvider)`; the answer is re-scoped to the point
  (`ResponseOverrideHook.ToRule`), read by `SurlServerFaults.Read` (the `--fault` reader) and
  checked against `FaultPlan.StageOf`. A timeout, a throw or a refused answer publishes
  `HookFailedEvent` and answers null. `SurlServerOptions.HookTimeLimit` is checked in
  `SurlServerArguments.ThrowIfMisused`.
- Layer-2 items (BL-668, ADR-0101 decisions 2 and 4): `Groups`, `MailDomains`, `Mailboxes`,
  `MailboxFiles`, `MailAliases`, `MailCatchAlls`, `SharedFolders`, `Shares`, `ShareAccess`,
  `Buckets`, `Topics`, `Queues`, `TopicMaps` and `TopicAccess` are `IList<string>`s, each item the
  option's value in `surl`'s own grammar, composed as that option once per item after the faults
  and the log level; `ConfigFiles` follow as `--config`, then `AdditionalArguments`. A null item or
  a `-` config file is an `ArgumentException`; every grammar refusal is `surl`'s, from the parser.
  Config files are read by `ApiConfigFileSource` through `ReadStartFile` (disk when null). Share
  directories are probed with the host's `CanOpenDataDirectory`.
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

A test that needs an extended server (STOMP) sets `AreExtensionsTurnedOn = true` on its options, which overrides the seams' `SURL_EXTENSIONS=0`. The STOMP broker refuses a CONNECT without `login`/`passcode` when no policy loosens it, so such a test seeds an account (`StoreOptions`) and logs in as it; a queue exists for `Messages.EnqueueAsync` only after a client SENDs or SUBSCRIBEs to `/queue/<name>`.
