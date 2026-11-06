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
- `RecordExchanges` (ADR-0099 decision 7, off by default and adding no word) gives the serving
  a `FanOutExchangeLogFactory` that passes every call to the levelled or trace log and to a
  `RecordingExchangeLogFactory`. `SurlServer.Exchanges` snapshots it as `SurlExchangeRecord`s
  (exchange ID, client, `StartedAt`, the `SurlExchangeChunk`s in order, `Received`, `Sent`,
  `IsTruncated`) and `DroppedExchangeCount` counts the oldest dropped past
  `MaxRecordedExchanges`; bytes past `MaxRecordedBytesPerExchange` (both directions together)
  are cut. With recording off both throw `InvalidOperationException`; a non-positive bound is an
  `ArgumentException`. Notes are not recorded, and a record names no listen URL.
- `SurlServerHostSeams.ReadStartFile` is handed to the composition's `ReadStartFile`, so tests
  give the `--user-file` and `--hostkey` files in memory (`ApiTestSeams.StartFiles`). The
  `--cert`, `--key` and `--cacert` files are still read from disk by `ServerTlsComposition`.

## What it references

`Surl.Composition`, `Surl.Cli`, `Surl.Content`, `Surl.Networking` (`SocketListenerFactory`),
`Surl.Output` and `Surl.Protocol.Abstractions`. It is a composition root: no protocol server or
horizontal library may reference it (ADR-0099 decision 11).

## How it is tested

`Application/Surl.Api.UnitTests`, with no network and no disk: `SurlServerHostSeams` is the
internal seam, and the tests start servers through `ApiTestSeams` - a `FakeListenerFactory`
that binds nothing and reports port 49731, an in-memory data directory, an in-memory lock table
that refuses a second start on a held data directory with 124 as `DataDirectoryLock` does, and
`SURL_EXTENSIONS=0`. `SurlServerHostSeams.Production` is what the public `StartAsync` uses:
socket listeners, `DataDirectoryProbe.CanOpen`, `DataDirectoryLock.Take` and the process's
environment.
