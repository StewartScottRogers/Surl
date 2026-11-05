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
- A start `surl` would refuse throws `SurlServerStartException`, with the `SurlExitCode` and the
  lines `surl` writes after `surl: `; nothing is left bound or locked.
- `StopAsync` or disposing the server stops it.

`Surl.Composition`'s public types, which this package carries, are not a supported programming
interface and may change in any release.
