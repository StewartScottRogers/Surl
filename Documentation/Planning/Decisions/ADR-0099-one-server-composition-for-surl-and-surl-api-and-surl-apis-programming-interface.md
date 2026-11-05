# ADR-0099 — One server composition for surl and Surl.Api, and Surl.Api's programming interface

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-611. Stewart decided that `Surl.Api` exists ([ADR-0098](ADR-0098-surl-api-and-surl-mcp-join-surl-as-products.md),
  BL-610); this ADR decides its design.
- **Builds on:** [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) (upstream curl is the
  only oracle), ADR-0004 section 5 (every byte reaches `IExchangeLog`), ADR-0006 (limits), ADR-0007
  section 5 (exit codes and `surl: ` texts), ADR-0031 decision 7 (the data-directory lock),
  ADR-0032 (secure by default), ADR-0033 (log levels), ADR-0046 (`--aihelp`), ADR-0079 (category
  folders) and ADR-0083 decisions 6 and 7 (`SURL_EXTENSIONS`, `ExtendedProtocolServers`).
- **Acted on by:** BL-609, BL-613, BL-614, BL-615, BL-616, BL-617, BL-618, BL-628, BL-643 and
  BL-649 (listed under Consequences). BL-612 decides `Surl.Mcp` on top of this ADR.

## Context

Today every protocol server is built in one place, `Application/Surl.Console`: `CommandLineRunner`
parses the command line, runs the ordered start checks, loads the service state, composes TLS, opens
the log streams, builds the `ServingEngine` and writes each `surl: ` message and exit code. ADR-0098
adds two more hosts. `Surl.Api` must start the same servers in-process, refuse the same starts with
the same exit codes and text, and be as closed by default as `surl`. Copying the composition into a
second host would let the two drift; that drift is exactly what a test suite using `Surl.Api` would
then measure instead of `surl`.

The Curl port's `Curl.Api` (ADR-0506 in the port) is a .NET-idiomatic interface over one shared
engine, packed as one NuGet package that bundles its project references. Its shape was read and is
copied where it fits; it is not evidence for anything here (ADR-0003). The reasons below are Surl's.

Constraints: base class library only; every production project native-AOT compatible, so no
reflection-based dependency injection; the four quality gates; MSTest only; protocol servers never
reference a host; tests pass on Windows, Linux and macOS; `TimeProvider` injected; publishing to
nuget.org is a release action that needs Stewart.

## Decision

### 1. The shared composition: `Surl.Composition`

A new class library `Application/Surl.Composition.UnitLibrary` (namespace `Surl.Composition`), tested
by `Application/Surl.Composition.UnitTests`, held to the quality gates like every `*.UnitLibrary`. It
is the one place protocol servers are composed; `Surl.Console` and `Surl.Api` reference it, and so may
`Surl.Mcp` (BL-612 decides whether through `Surl.Api`). It sits in `Application/` because it is part of
the hosts, not a service every server may use (ADR-0079): it references every protocol server, which no
`Foundation/` library may.

**Moves to `Surl.Composition`**, keeping their names (BL-609 and BL-613 move them; BL-614 the start
sequence):

| From `Surl.Console` | Moved by |
| --- | --- |
| `CommandLineRunner.ComposeProtocolServers` (name kept; 26 Backlog tasks name it), `ComposeUnservedProtocolServers`, `ComposeRegisteredSchemes`, `ComposeExtendedSchemes`, `ComposeExtendedProtocolServers`, the `ServiceState` record, `ImplicitTlsSchemeServer` | BL-609 |
| `ExtendedProtocolServers` (`Compose` and `AreTurnedOn` keep their names), `ExtendedProtocolServerServices`, `IEnvironmentVariableReader`, `ProcessEnvironmentVariableReader` | BL-609 |
| `ComposeContentStore`, `ComposeContentFileSystem`, `MapExposureOptions`, `ComposeRetainedMessageFile`, `LoadRetainedMessagesAsync`, `ComposeMailStoreFiles`, `LoadMailStoreAsync`, `ComposeLdapDirectoryFile`, `LoadLdapServerAsync`, `ComposeConnectionLimits` | BL-609 |
| `AuthenticationComposition`, `KeytabComposition`, `SshHostKeyComposition`, `SshAlgorithmComposition`, `ServerTlsComposition`, `DataDirectoryLock`, `DataDirectoryLockOutcome`, `DataDirectoryProbe`, `RandomKerberosRandomSource`, `DescribeTlsFileFailure` | BL-613 |
| `FindOptionRefusal`, `FindListenUrlRefusal`, `FormatMissingCertificate`, `FormatBindFailure`, `DescribeBindFailure`, the ordered start (`ServeAvailableAsync` to `ServeLoggedAsync`), `ListenerStartReporter`, the start-up warning writers (`WriteLooseningWarnings`, `WriteStartLines`, the throwaway-certificate lines) | BL-614 |

Each composition that refuses a start returns its refusal as a `SurlExitCode` and the lines `surl`
writes after `surl: `, instead of writing them; the warning writers keep taking the log `TextWriter` and
the level, so their bytes do not change.

**Stays in `Surl.Console`:** `Program` (signals, `Program.RunAsync`), `CommandLineRunner.RunAsync` with
parsing through `Surl.Cli`, `--help`, `--aihelp`, `--manual`, `--version` (`ComposeVersionText`, which
reads `Surl.Composition`'s scheme lists), `HideAtLevelNone`, `WriteHelp`, `WriteText`, `WriteRefusal`,
`WriteFailure`, `LogFile`, `LogStreams`, and every write of a `surl: ` message and every exit code.
`CommandLineRunnerAiHelpTests` stays in `Surl.Console.UnitTests`, reading the schemes through
`Surl.Composition`.

`Surl.Composition`'s types are `public`, because two hosts in two assemblies call them, but they are not
`Surl.Api`'s supported surface: its README says so and they may change in any release.

### 2. The composition's input and output

```csharp
namespace Surl.Composition;

public static class SurlServerComposition
{
    public static IReadOnlyList<string> ComposeRegisteredSchemes(TimeProvider timeProvider);
    public static IReadOnlyList<string> ComposeExtendedSchemes(bool areExtensionsTurnedOn);
    public static Task<SurlServerStart> ComposeAsync(
        SurlCommandLine commandLine, SurlServerCompositionSeams seams, CancellationToken cancellationToken);
}

public sealed record SurlServerCompositionSeams(
    Func<ServerTlsSettings?, IListenerFactory> CreateListenerFactory,
    Func<string, bool> CanOpenDataDirectory,
    Func<string, DataDirectoryLockOutcome> TakeDataDirectoryLock,
    TimeProvider TimeProvider,
    bool AreExtensionsTurnedOn)
{
    public IContentFileSystem? DataDirectoryFileSystem { get; init; }          // DiskContentFileSystem when null
    public IContentFileSystem? ContentWithoutDataDirectory { get; init; }      // a new, empty InMemoryContentFileSystem when null
    public Func<string, byte[]>? ReadStartFile { get; init; }                   // File.ReadAllBytes when null
    public Func<ServerTlsSettings?, IDataConnectionOpener>? CreateDataConnectionOpener { get; init; }
    public Func<ExtendedProtocolServerServices, IReadOnlyList<IProtocolServer>>? ComposeExtendedProtocolServers { get; init; }
}

public sealed record SurlServerStart(SurlStartFailure? Failure, ComposedServing? Serving);   // exactly one is set

public sealed record SurlStartFailure(SurlExitCode ExitCode, IReadOnlyList<string> Lines);    // each line as written after "surl: "

public sealed class ComposedServing : IAsyncDisposable                                         // disposing releases the data-directory lock
{
    public IReadOnlyList<string> ThrowawayCertificateFingerprints { get; }
    public Task<SurlServingOutcome> ServeAsync(SurlServingOutputs outputs, CancellationToken cancellationToken);
}

public sealed record SurlServingOutputs(
    TextWriter Log,                                   // start-up warnings, at the command line's level
    TextWriter StatusOutput,                          // the status lines (ListenerStatusLine)
    IExchangeLogFactory ExchangeLogFactory,
    Action<IReadOnlyList<ListenUrl>> AllListenersBound);   // each listen URL with its bound port, in listen order, once

public sealed record SurlServingOutcome(SurlExitCode ExitCode, SurlStartFailure? Failure);  // a bind failure's (45) or (6), or (125)
```

- **Input** is `Surl.Cli`'s parsed `SurlCommandLine`, so `Surl.Api` and `surl` validate identically,
  plus the seams `CommandLineRunner` takes today. `SURL_EXTENSIONS` is no longer read inside the
  runner's constructor: each host reads it (`ExtendedProtocolServers.AreTurnedOn`) and passes
  `AreExtensionsTurnedOn`. `ContentWithoutDataDirectory` is new: it is how `Surl.Api` serves files the
  caller supplies in memory.
- **The split at the log streams.** `ComposeAsync` runs the ordered start up to and including server TLS
  and the service state; it never opens a file for writing. `surl` then opens its log streams
  (`LogStreams`, 23 on failure, disposing the `ComposedServing` so the lock is released) and calls
  `ServeAsync`, which writes the start-up warnings to `Log`, builds the `ServingEngine` and serves until
  cancelled. So the order and bytes of every `surl` message are what they are today.
- **Bound URLs.** `ListenerStartReporter`, moved, collects each listener's bound `ListenUrl` (the
  `IListenerFactory` listeners carry the bound port) and calls `AllListenersBound` once the last listener
  has bound, the moment it writes the status lines today. A bind failure is reported in
  `SurlServingOutcome.Failure` and `AllListenersBound` is never called.

### 3. `Surl.Api`'s project

`Application/Surl.Api` (assembly and namespace `Surl.Api`), tested by `Application/Surl.Api.UnitTests`.
It carries no `.UnitLibrary` suffix: it is a product named for itself, a naming exception like
`Surl.Console` (root `CLAUDE.md`, ADR-0098), and is held to the same quality gates. There is no
`Surl.Api.UnitLibrary`, because `Surl.Composition` is the shared core; `Surl.Api` is the thin public
face over it. It references `Surl.Composition`, `Surl.Cli`, `Surl.Networking` (for
`SocketListenerFactory` and `SocketDataConnectionOpener`) and `Surl.Output`.

### 4. The public surface

```csharp
namespace Surl.Api;

public sealed class SurlServer : IAsyncDisposable
{
    public static Task<SurlServer> StartAsync(SurlServerOptions options, CancellationToken cancellationToken = default);
    public IReadOnlyList<Uri> BoundUrls { get; }      // each listen URL in the order given, port 0 replaced by the bound port
    public Uri BoundUrl { get; }                      // BoundUrls[0]
    public IReadOnlyList<SurlExchangeRecord> Exchanges { get; }   // decision 7
    public long DroppedExchangeCount { get; }                     // decision 7
    public Task<SurlExitCode> StopAsync(CancellationToken cancellationToken = default);
    public ValueTask DisposeAsync();                  // StopAsync, its exit code discarded; idempotent
}

public sealed class SurlServerOptions
{
    // What to serve (surl's listen URLs, as text: "http://127.0.0.1:0").
    public IList<string> ListenUrls { get; }

    // Content (ADR-0031): a data directory, or files supplied in memory; not both.
    public string? DataDirectory { get; set; }                                   // --directory
    public IDictionary<string, byte[]> Files { get; }                            // in-memory content, path relative to the root, '/' separated
    public bool AllowUploads { get; set; }                                       // --allow-uploads
    public bool ListDirectories { get; set; }                                    // --list-directories
    public bool FollowSymlinks { get; set; }                                     // --follow-symlinks
    public bool ServeDotFiles { get; set; }                                      // --serve-dot-files

    // Accounts and logins (ADR-0032).
    public IList<SurlAccount> Accounts { get; }                                  // --user, in order
    public string? UserFile { get; set; }                                        // --user-file
    public IList<string>? AuthenticationMethods { get; set; }                    // --auth words; null is surl's default set
    public bool AllowAnonymous { get; set; }                                     // --allow-anonymous
    public bool AllowPlaintextAuthentication { get; set; }                       // --allow-plaintext-auth

    // TLS (ADR-0010, ADR-0020).
    public string? CertificateFile { get; set; }                                 // --cert
    public string? KeyFile { get; set; }                                         // --key
    public string? KeyPassphrase { get; set; }                                   // --pass
    public bool SelfSigned { get; set; }                                         // --self-signed
    public string? CaCertificateFile { get; set; }                               // --cacert

    // SSH (ADR-0051).
    public IList<string> HostKeyFiles { get; }                                   // --hostkey, in order
    public bool ThrowawayHostKey { get; set; }                                   // --throwaway-hostkey

    // Limits (ADR-0006); null leaves surl's default.
    public int? MaxConnections { get; set; }                                     // --max-connections
    public int? MaxConnectionsPerAddress { get; set; }                           // --max-connections-per-address
    public TimeSpan? IdleTimeout { get; set; }                                   // --idle-timeout
    public TimeSpan? MaxTime { get; set; }                                       // -m/--max-time
    public long? MaxFileSize { get; set; }                                       // --max-filesize

    // Any other surl option, as surl's own words, read after the properties above.
    public IList<string> AdditionalArguments { get; }

    // The host, not the command line.
    public TextWriter? Log { get; set; }                                         // null: silent
    public LogLevel LogLevel { get; set; } = LogLevel.Info;                      // --log-level, used only with Log
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    public bool? AreExtensionsTurnedOn { get; set; }                             // null: read SURL_EXTENSIONS at StartAsync
    public bool RecordExchanges { get; set; }                                    // decision 7
    public int MaxRecordedExchanges { get; set; } = 1000;
    public int MaxRecordedBytesPerExchange { get; set; } = 1_048_576;
}

public sealed record SurlAccount(string UserName, string Password);
```

- **Every command-line property maps onto `surl`'s own words, and the words are parsed.**
  `StartAsync` turns the options into an argument list - each listen URL, then each property that
  differs from its default as the option it names (`--user name:password`, `--max-time 30`, seconds in
  invariant culture, `--auth` once with the words joined by commas), then `AdditionalArguments` - and
  parses it with `CommandLineParser.Parse` (with the extended schemes when turned on) into the
  `SurlCommandLine` `Surl.Composition` composes. So the API validates exactly as `surl` does, every
  default is `surl`'s, and whatever BL-641 decides about `surl`'s configuration surface stays reachable:
  a property is only a spelling of an option.
- **No `string[]` overload of `StartAsync`.** `AdditionalArguments` is that overload: a test that wants
  `--keytab`, `--authorized-keys`, `--hostcert`, `--ssh-ciphers`, `--ws-echo` or any option added later
  writes `surl`'s words, and only options a test commonly needs get typed properties. One entry point
  keeps one validation path.
- **Arguments a host owns** are `ArgumentException`s, not starts: `--help`, `--aihelp`, `--manual`,
  `--version` (no server to start), and `--log-file` and `--trace`/`--trace-ascii` (the API's log is
  `Log`; it writes no files of its own choosing). So are `DataDirectory` together with `Files`, a
  `Files` path that is empty, absolute or climbs with `..`, an empty `ListenUrls`, a non-positive
  `MaxRecordedExchanges` or `MaxRecordedBytesPerExchange`, and a null argument.
- **`StartAsync` returns once every listener has bound** (`AllListenersBound`), with `BoundUrls` set. It
  serves on `Surl.Networking`'s `SocketListenerFactory`, the same as `surl`. The `CancellationToken`
  cancels the start only: a start cancelled before it binds throws `OperationCanceledException` and
  releases everything it took.
- **`StopAsync`** cancels serving and returns the exit code `surl` would have exited with after Ctrl+C
  (`Ok`, or `InternalError` when serving failed), after the same shutdown grace period.

### 5. Failures

```csharp
public sealed class SurlServerStartException : Exception
{
    public SurlExitCode ExitCode { get; }
    public IReadOnlyList<string> Lines { get; }   // each as surl writes it after "surl: "
    // Message: the lines joined with "\n"
}
```

Every start `surl` refuses - a command-line refusal (1 or 2), a data directory it cannot open (37), a
TLS-first listen URL without a certificate (58), an unreadable or malformed start file (37, 2, 58, 77),
the lock held (124), a bind failure (45 or 6) - throws `SurlServerStartException` from `StartAsync` with
the same `SurlExitCode` and text, and leaves nothing bound or locked. Misuse of the API (decision 4's
list) is `ArgumentException` or `ArgumentNullException`. No other exception type is public.

### 6. Secure by default

The API's defaults are the command line's, because its options are parsed as command-line words
(ADR-0032, ADR-0006): no anonymous login, no plaintext authentication, no uploads, no directory listings,
no symlinks or dot files, surl's limits, `--auth`'s default set. Each loosening is a property named after
its option (`AllowAnonymous`, `AllowPlaintextAuthentication`, `AllowUploads`, `SelfSigned`,
`ThrowawayHostKey`), and through `AdditionalArguments` the option itself. The start-up warnings
(`--allow-anonymous`, `--allow-plaintext-auth`, `--auth`, `--self-signed`, `--throwaway-hostkey`,
`--allow-weak-ssh-algorithms`, keytab lines) and the status lines go to `Log` at `LogLevel` (ADR-0033),
byte for byte as `surl` writes them; with `Log` null the API is silent. The exchange log at `-v` and
above also goes to `Log`.

### 7. What a client sent

With `RecordExchanges` set, `Surl.Api` adds an internal `RecordingExchangeLogFactory` beside the
levelled log (both receive every call, ADR-0004 section 5), and `SurlServer.Exchanges` returns a snapshot:

```csharp
public sealed record SurlExchangeRecord(
    long ExchangeId,
    EndPoint Client,
    DateTimeOffset StartedAt,                       // from TimeProvider, when the exchange's log was created
    IReadOnlyList<SurlExchangeChunk> Chunks,        // in the order the bytes crossed the connection
    bool IsTruncated)
{
    public byte[] Received { get; }                 // every Received chunk, concatenated
    public byte[] Sent { get; }
}

public sealed record SurlExchangeChunk(SurlExchangeDirection Direction, ReadOnlyMemory<byte> Bytes);

public enum SurlExchangeDirection { Received, Sent }
```

- **Bounds.** At most `MaxRecordedExchanges` exchanges (default 1000); past it the oldest is dropped and
  `DroppedExchangeCount` grows. At most `MaxRecordedBytesPerExchange` bytes (default 1 MiB, both
  directions together) per exchange; past it no more bytes are kept for that exchange and `IsTruncated`
  is true. Memory is therefore bounded at about 1 GiB at the defaults, and a test that needs more raises
  the bounds by name.
- **Off by default, and free when off.** Without `RecordExchanges` no recording factory exists, and
  `Exchanges` throws `InvalidOperationException` saying `RecordExchanges` is off - an empty list would
  read as "the client sent nothing".
- **No listen URL on a record.** `IExchangeLogFactory.Create` receives the exchange ID and the client's
  endpoint, not the listener, and widening that shared contract for one host is not worth it: a test
  serving several listen URLs tells them apart by what was sent. This departs from BL-617's Goal.
- `IExchangeLog.Note` texts are not recorded; they are `surl`'s log lines, which `Log` carries.

### 8. Process-wide state

- **Several `SurlServer`s may run in one process**, each with its own composition, accounts, stores,
  TLS settings, Kerberos replay cache and `RandomKerberosRandomSource`. Nothing in `Surl.Composition` is
  static state.
- **The data-directory lock still holds** (ADR-0031 decision 7): `DataDirectoryLock` opens
  `.surl/lock` exclusively, so a second server on the same path in the same process or another refuses
  with 124, as a second `surl` does. In-memory content takes no lock.
- **`SURL_EXTENSIONS`** is read from the process at `StartAsync` when `AreExtensionsTurnedOn` is null;
  `true` or `false` overrides it for that server only.
- **`TimeProvider`** is injectable through `SurlServerOptions.TimeProvider` and reaches the composition,
  the serving engine and the record's `StartedAt`.

### 9. Agent-facing help for `Surl.Api`

`Surl.Api`'s help is its XML documentation (every public member, as `GenerateDocumentationFile` already
requires) and a package `Application/Surl.Api/README.md` packed as the package README: a quick start
(start on `http://127.0.0.1:0`, read `BoundUrl`, stop), the option-to-property table, the failures and
the exchange record. That is enough: a .NET caller reads the IDE's documentation and the package page,
and the options' meanings are `surl`'s, already in `surl --aihelp` and `surl --manual`. `surl --aihelp`
documents the command line (ADR-0046), and its overview gains one sentence each pointing at `Surl.Api`
and `Surl.Mcp` and where their help lives (BL-628); no `--aihelp` topic describes the API.

### 10. Packaging

`Surl.Api` is packable (`IsPackable` true in its project only; `Directory.Build.props` keeps false for
the rest) as one NuGet package, ID `Surl.Api`, that bundles its project references' assemblies
(`Surl.Composition`, `Surl.Cli`, every protocol server and horizontal library) in `lib/net10.0` with no
package dependency, so a test project references one package and restores nothing else. Its version is
the version `release.yml` gives `surl`. `release.yml` packs it and keeps the `.nupkg` as a workflow
artifact - a dry run. Pushing it to nuget.org is a release action that needs Stewart (root `CLAUDE.md`,
"Git and GitHub"); no task plans the push. Built by BL-643.

### 11. The reference graph

`Surl.Composition`, `Surl.Api` and the `Surl.Mcp` projects are composition roots, as `Surl.Console` is.
No protocol server, extended server or horizontal library (`Foundation/`, `Cryptography/`, the shared
protocol libraries) may reference any of them, and `Surl.Composition` references no host (`Surl.Console`,
`Surl.Api`, `Surl.Mcp`). `ProtocolIsolationTests` already refuses a protocol server referencing anything
off its allowed list, the hosts included; BL-609 adds `Surl.Composition` and `Surl.Api` rows to
`ForbiddenProtocolReferences_OtherProject_IsForbidden` and a test that no project outside `Application/`
references a composition root, and that `Surl.Composition` references no host.

## Consequences

- `Surl.Console` becomes parsing, help and writing; `surl`'s output and exit codes do not change, which
  `Surl.Console.UnitTests` and `Surl.Conformance` keep proving while the code moves.
- A server started through `Surl.Api` answers the same bytes as `surl` with the same options, because it
  is composed by the same code from the same parsed `SurlCommandLine`; BL-618 proves it against pinned
  upstream curl 8.21.0. Upstream curl stays the only oracle.
- The follow-up tasks, filed with BL-611, and where this ADR departs from the planning assumptions in
  BL-611:
  - **BL-609** creates `Surl.Composition` and moves the server composition (decision 1's BL-609 rows).
    Departure: it also adds decision 11's reference-graph tests, so its `touches` gains
    `Foundation/Surl.Protocol.Abstractions.UnitTests`.
  - **BL-613** moves the authentication, keytab, host key, SSH algorithm and TLS compositions and the
    data-directory probe and lock, each returning `SurlStartFailure`-shaped refusals.
  - **BL-614** moves the start sequence behind `SurlServerComposition.ComposeAsync` and
    `ComposedServing.ServeAsync`. Departures: the start is split at the log streams (decision 2), so
    `LogStreams` stays in `Surl.Console` and `ServeAsync` writes the start-up warnings; `SURL_EXTENSIONS`
    is read by the host and passed in; and the new `ContentWithoutDataDirectory` seam is added.
  - **BL-615** creates `Surl.Api` with `SurlServer`, `SurlServerOptions` (listen URLs, content including
    in-memory `Files`, `AdditionalArguments`, the host properties), `SurlServerStartException`, `StartAsync`,
    `BoundUrls`, `StopAsync` and `DisposeAsync`. Departure: in-memory `Files` are decided here and built
    by BL-615, since its start needs content.
  - **BL-616** adds the account, `--auth`, loosening, TLS, host key and limit properties of decision 4.
  - **BL-617** adds `RecordExchanges`, the bounds, `Exchanges`, `DroppedExchangeCount` and the record
    types. Departure: a record carries no listen URL (decision 7), so its Goal's "its listen URL" is
    dropped.
  - **BL-618** proves a `Surl.Api` server against pinned upstream curl, the record holding curl's bytes.
  - **BL-628** points `surl --aihelp`'s overview at `Surl.Api` (decision 9).
  - **BL-643** packs `Surl.Api` as decision 10 says, dry run only.
  - **BL-649** documents `Surl.Api` once it exists.
- BL-641 (`surl`'s configuration surface), named in BL-611's Notes, is not on the board in this
  checkout; whatever it decides, decision 4 keeps every property a spelling of a command-line option.
- `Surl.Composition`'s public types are not a supported API; changing them is not a breaking change of
  `Surl.Api`.
