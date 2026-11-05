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
- Accounts, `--auth`, TLS, host keys and limits as typed properties are BL-616's; recording
  exchanges is BL-617's. Until then those options are reachable through `AdditionalArguments`.

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
