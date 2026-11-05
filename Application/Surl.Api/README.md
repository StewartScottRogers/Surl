# Surl.Api

Starts and stops a Surl server in-process, for a .NET test suite, with no `surl` process.

```csharp
using Surl.Api;

var options = new SurlServerOptions { ListenUrls = { "http://127.0.0.1:0" } };
options.Files["hello.txt"] = "hello"u8.ToArray();

await using var server = await SurlServer.StartAsync(options);
// server.BoundUrl is http://127.0.0.1:<the bound port>/
```

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

- Nothing is looser than `surl`: with only a listen URL and an account, anonymous logins,
  plaintext authentication and uploads are all off. Each loosening is a property named for its
  option, and each writes `surl`'s start-up warning to `Log`.
- A start `surl` would refuse throws `SurlServerStartException`, with the `SurlExitCode` and the
  lines `surl` writes after `surl: `; nothing is left bound or locked.
- `StopAsync` or disposing the server stops it.

## What a client sent

Set `RecordExchanges` and read `server.Exchanges` to see each exchange's bytes, oldest first:

```csharp
options.RecordExchanges = true;
await using var server = await SurlServer.StartAsync(options);
// ... run the client ...
var request = server.Exchanges[0].Received;   // every byte the client sent
var response = server.Exchanges[0].Sent;      // every byte the server answered
```

Each `SurlExchangeRecord` has the exchange ID, the client's endpoint, `StartedAt` (from
`TimeProvider`), the `Chunks` in the order the bytes crossed (one per read or write, one per
datagram), and `IsTruncated`. At most `MaxRecordedExchanges` (1000) are kept, the oldest dropped
and counted in `DroppedExchangeCount`; at most `MaxRecordedBytesPerExchange` (1 MiB, both
directions together) of each, the rest cut. Recording is off by default and costs nothing then;
reading `Exchanges` with it off throws `InvalidOperationException`. A record names no listen URL:
tell several listeners apart by what was sent.

`Surl.Composition`'s public types, which this package carries, are not a supported programming
interface and may change in any release.
