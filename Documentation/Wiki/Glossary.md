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
| hardening limit | A bound on what a peer can make Surl hold - connections, time, bytes - with a default number, an option to change it, and 0 meaning no limit (ADR-0006). | `ExchangeLimits` for the per-exchange ones; not yet for the rest |
| idle timeout | How long an exchange may go with no byte moving in either direction on any of its transports before Surl closes it. 120 s by default (ADR-0006). | not yet |
| head timeout | How long a peer has to deliver a complete request head, command line or first packet; the slow-sender defence. 30 s by default (ADR-0006). | `ExchangeLimits.HeadTimeout` (not yet) |
| connection refusal | The protocol's own "too busy" answer written to a connection or datagram flow accepted past a connection limit, before it is closed (ADR-0006). | `IConnectionRefusalWriter`, `IDatagramRefusalWriter`, `ConnectionRefusal` (not yet) |
| exposure default | What a server offers until an option says otherwise: uploads, directory listings, symbolic links, dot-files. All off by default (ADR-0006). | not yet |
| answered as absent | Refused with the protocol's not-found answer, identical to the answer for a path that does not exist, so a peer cannot tell hidden from missing (ADR-0006). | not yet |
| escaped rendering | How the verbose log shows bytes: printable ASCII except backslash as itself, CR and LF as `\r` and `\n`, every other byte as `\xHH`, so no peer byte reaches the terminal as a control sequence (ADR-0006). | not yet |

## Building and testing

| Term | Meaning | Name in code |
| --- | --- | --- |
| exit code | The number the `surl` process returns. Reuses upstream curl's `CURLE_*` number wherever a server-side meaning carries over; the table is ADR-0005. | `SurlExitCode` (ADR-0005) |
| fast tests | Every test that needs no network: `dotnet test --filter "TestCategory!=Integration"`. | — |
| integration test | A test that starts a real process or opens a real socket, such as one that runs pinned upstream curl. | `[TestCategory("Integration")]` |
| byte script | The ordered bytes of an exchange - what upstream curl sends, what Surl sends back - measured with `Record-CurlExchange.ps1` and replayed through a fake connection in a protocol server's tests. | — |
