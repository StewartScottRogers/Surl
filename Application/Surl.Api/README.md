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

`Surl.Composition`'s public types, which this package carries, are not a supported programming
interface and may change in any release.
