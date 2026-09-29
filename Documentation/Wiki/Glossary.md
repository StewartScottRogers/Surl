# Glossary

One term, one meaning, one name in code. When code or a document uses one of these
terms, it means exactly what is written here; when a concept below needs naming in code,
it uses the name in the "Name in code" column. "Not yet" means Phase 1 or later names it.

## Two programs named curl

| Term | Meaning | Name in code |
| --- | --- | --- |
| upstream curl | The original C implementation of curl, https://github.com/curl/curl. Surl's only oracle (ADR-0003). Always "upstream curl" when the distinction matters. | — |
| the Curl port | Stewart's C# port of curl, https://github.com/StewartScottRogers/Curl. Measured against Surl in the last phase; never used to validate Surl (ADR-0003). Always "the Curl port", never bare "Curl" when it could be misread. | — |
| pinned upstream build | An upstream curl executable identified by its SHA-256 in `UpstreamCurlBuilds.json`. The only kind of curl Surl's tools run. | `UpstreamCurlBuilds.json`, `Assert-PinnedUpstreamCurl` |
| reference release | The upstream curl release Surl is measured against: 8.21.0, released 2026-06-24, the release the Curl port targets. | — |

## Serving

| Term | Meaning | Name in code |
| --- | --- | --- |
| mate | The server that answers a given curl request. Surl is curl's mate. | — |
| listen URL | A URL given to `surl`: its scheme picks the protocol server, its host and port the bind address. | `ListenUrl` (ADR-0004) |
| protocol server | The library that answers one protocol family, secure variants included: `Surl.Protocol.<Name>.UnitLibrary`. | `IProtocolServer`, implemented through `IConnectionProtocolServer` or `IDatagramProtocolServer` (ADR-0004) |
| horizontal library | A library a protocol server may reference besides Abstractions, listed in ADR-0002's table. | `HorizontalLibraries` in `ProtocolIsolationTests` |
| data directory | The directory `--directory` names: its files are what every file-serving protocol server serves, and its `.surl` folder holds every other kind of service state, both surviving restarts. Without `--directory` there is none: surl serves an empty in-memory file system and keeps service state in memory (ADR-0031 decision 1, superseding ADR-0007's default of the current directory). Formerly "served directory". | `SurlCommandLine.DataDirectory` (`null` without `--directory`) |
| served root | The full path the content store is rooted at: the data directory's full path with `--directory`, `InMemoryContentFileSystem.RootPath` without it (ADR-0031 decision 1, ADR-0018). `ContentStore.ServedRoot` |
| in-memory file system | The file system the content store serves without `--directory`: empty at start, lasting for the process lifetime, bounded by `InMemoryContentFileSystem.DefaultMaxTotalBytes` (256 MiB) and never touching the disk. Past the bound a write fails as on a full disk (ADR-0031 decision 4). The second `IContentFileSystem` implementation beside `DiskContentFileSystem`. | `InMemoryContentFileSystem` |
| service-state folder | `<data directory>/.surl`: where services keep state other than served files (`.surl/mqtt/retained-messages` for MQTT retained messages) and where the data-directory lock lives. Never served, listed or uploaded to, whatever `--serve-dot-files` and `--follow-symlinks` say; the match is the first path segment, case-insensitive (ADR-0031 decisions 5 and 6). | `DataDirectoryLock.StateFolderName` in `Surl.Console`; hidden by `ContentStore` |
| data-directory lock | `<data directory>/.surl/lock`, held open exclusively for as long as surl serves, so one surl process uses a path at a time. A second process given the same path gets `DataDirectoryInUse` (124); a `.surl` or lock file that cannot be created gets `CouldNotWriteFile` (23). The operating system releases it when the holder exits; the file is never deleted (ADR-0031 decision 7). | `DataDirectoryLock.Take`, returning a `DataDirectoryLockOutcome` |
| listener status line | The line `Listening on <scheme>://<host>:<bound port>/` surl writes to stdout for each listen URL once every listener has bound (ADR-0007). | not yet |
| verbose exchange log | What `-v` writes to stderr: one line per exchange event, `#<exchange id> <marker> <text>`, the marker `<` for bytes received, `>` for bytes sent, `*` for a note (ADR-0007). | `IExchangeLog`, `IExchangeLogFactory` (ADR-0004), implemented in `Surl.Output` (not yet) |
| parsed command line | What `Surl.Cli` returns for a command line that serves: the listen URLs and every option value, defaults applied (ADR-0007). | `SurlCommandLine` |
| negatable option | A flag that `--no-<name>` turns off, as curl's boolean options are; the later of the two wins (ADR-0007). | not yet |
| content store | The file tree under the served root that every file-serving protocol server publishes, read and written through an `IContentFileSystem`, and the rules that map request paths onto it without escaping it. | `ContentStore` in `Surl.Content.UnitLibrary` |
| exchange | One conversation between upstream curl and Surl, over one connection or one datagram flow, from accept to close. One `ServeAsync` call. | `ExchangeContext` (ADR-0004) |
| hardening limit | A bound on what a peer can make Surl hold - connections, time, bytes - with a default number, an option to change it, and 0 meaning no limit (ADR-0006). | `ExchangeLimits` for the per-exchange ones; not yet for the rest |
| idle timeout | How long an exchange may go with no byte moving in either direction on any of its transports before Surl closes it. 120 s by default (ADR-0006). | not yet |
| head timeout | How long a peer has to deliver a complete request head, command line or first packet; the slow-sender defence. 30 s by default (ADR-0006). | `ExchangeLimits.HeadTimeout` (not yet) |
| connection refusal | The protocol's own "too busy" answer written to a connection or datagram flow accepted past a connection limit, before it is closed (ADR-0006). | `IConnectionRefusalWriter`, `IDatagramRefusalWriter`, `ConnectionRefusal` (not yet) |
| exposure default | What a server offers until an option says otherwise: uploads, directory listings, symbolic links, dot-files. All off by default (ADR-0006). | not yet |
| answered as absent | Refused with the protocol's not-found answer, identical to the answer for a path that does not exist, so a peer cannot tell hidden from missing (ADR-0006). | not yet |
| escaped rendering | How the verbose log shows bytes: printable ASCII except backslash as itself, CR and LF as `\r` and `\n`, every other byte as `\xHH`, so no peer byte reaches the terminal as a control sequence (ADR-0006). | not yet |

## HTTP

| Term | Meaning | Name in code |
| --- | --- | --- |
| request head | Everything an HTTP/1.x request sends before its body: the request line, then zero or more field lines, then the empty line that ends them (RFC 9112, sections 2.1 and 3). Empty lines before the request line are skipped (RFC 9112, section 2.2). Bounded by the request-head limit (ADR-0006). | `HttpRequestHead`, read by `HttpConnectionReader`, which reports how the read ended as an `HttpRequestHeadReadOutcome` |
| request line | The first line of a request head, `method SP request-target SP HTTP-version` (RFC 9112, section 3). | `HttpRequestHead.Method`, `HttpRequestHead.RequestTarget`, `HttpRequestHead.Version`; parsed by `HttpRequestLineParser` |
| field line | One header field in a request head, `field-name ":" OWS field-value OWS`, the whitespace around the value not part of it (RFC 9112, section 5). | `HttpRequestField`; parsed by `HttpFieldLineParser`; a head's field lines are `HttpRequestHead.Fields` |

## Authentication

| Term | Meaning | Name in code |
| --- | --- | --- |
| checked login | A login whose credentials were checked against the accounts, accepted or refused, and the only kind the verbose log notes: `Login accepted: <method> <user>` or `Login refused: <method> <user>`, `<method>` the HTTP `Authorization` scheme or the listen URL's scheme, `<user>` as sent (`bearer token` for Bearer, left out when unreadable). No credentials, a plain-text secret refused unchecked, no user name, a handshake's continuation step and `--allow-anonymous` are not checked logins (ADR-0032 section 8, ADR-0038). | `CheckedLogin` in `Surl.Protocol.Abstractions`, on `HttpAuthenticationVerdict.CheckedLogin`; a password login unchecked under `--allow-anonymous` is `PasswordLoginVerdict.AcceptedUnchecked` |

## Building and testing

| Term | Meaning | Name in code |
| --- | --- | --- |
| exit code | The number the `surl` process returns. Reuses upstream curl's `CURLE_*` number wherever a server-side meaning carries over; the table is ADR-0005. | `SurlExitCode` (ADR-0005) |
| fast tests | Every test that needs no network: `dotnet test --filter "TestCategory!=Integration"`. | — |
| integration test | A test that starts a real process or opens a real socket, such as one that runs pinned upstream curl. | `[TestCategory("Integration")]` |
| byte script | The ordered bytes of an exchange - what upstream curl sends, what Surl sends back - measured with `Record-CurlExchange.ps1` and replayed through a fake connection in a protocol server's tests. | — |
