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
| content store | The directory tree a Surl server publishes, and the rules that map request paths onto it without escaping it. | `Surl.Content.UnitLibrary` |
| exchange | One conversation between upstream curl and Surl, over one connection or one datagram flow, from accept to close. One `ServeAsync` call. | `ExchangeContext` (ADR-0004) |

## Building and testing

| Term | Meaning | Name in code |
| --- | --- | --- |
| exit code | The number the `surl` process returns. Reuses upstream curl's `CURLE_*` number wherever a server-side meaning carries over; the table is ADR-0005. | `SurlExitCode` (ADR-0005) |
| fast tests | Every test that needs no network: `dotnet test --filter "TestCategory!=Integration"`. | — |
| integration test | A test that starts a real process or opens a real socket, such as one that runs pinned upstream curl. | `[TestCategory("Integration")]` |
| byte script | The ordered bytes of an exchange - what upstream curl sends, what Surl sends back - measured with `Record-CurlExchange.ps1` and replayed through a fake connection in a protocol server's tests. | — |
