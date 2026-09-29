# Requirements

Each requirement gets a stable identifier so planning, commits and tests can cite
it. Identifiers are never reused or renumbered, even after a requirement is
dropped — mark it `Withdrawn` instead. A functional requirement names the upstream curl
option or request it answers, and the upstream curl build its behaviour was measured
against (`UpstreamCurlBuilds.json`) - never the Curl port (ADR-0003).

The requirements below are Phase 1's, derived from the Phase 1 row of
[the product overview](Product-Overview.md)'s "Phasing", its "Success criteria" and
"Constraints", and the Accepted ADRs in `Documentation/Planning/Decisions/`. Nothing here
states what those sources do not.

## Functional

"Measured against" names the pinned build a row's behaviour is measured with. Every
Phase 1 row names the reference build: upstream curl 8.21.0, the Git for Windows mingw64
build at `C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, written below as
**curl 8.21.0 (Git for Windows)**. Where an ADR has already measured the behaviour, the
row cites it; the rest are measured by the task that implements them, and a row stays
`Draft` until that measurement is in a test.

| ID | Requirement | Answers (upstream curl) | Measured against | Priority | Status |
| --- | --- | --- | --- | --- | --- |
| FR-001 | `surl http://<host>:<port>/` serves the served directory over HTTP/1.1 and HTTP/1.0: a `GET` of a file answers its bytes with a status line and headers upstream curl completes the transfer on, exit 0 (product overview, Phase 1 "Proves"; success criterion 1). | `curl http://<host>:<port>/<path>` (a `GET`), and with `-0`/`--http1.0` | curl 8.21.0 (Git for Windows) | Must | Draft |
| FR-002 | The HTTP server answers `HEAD` for a file with the headers the `GET` would carry and no body. | `curl -I`/`--head http://<host>:<port>/<path>` | curl 8.21.0 (Git for Windows) | Must | Draft |
| FR-003 | A request path never leaves the served directory; a path that does not exist, or is hidden by an exposure default (FR-011), gets the protocol's not-found answer (`Surl.Content.UnitLibrary/CLAUDE.md`; ADR-0006 section 2). | `curl http://<host>:<port>/<path>` with `..` segments or a missing file | curl 8.21.0 (Git for Windows) | Must | Draft |
| FR-004 | A listen URL is `scheme://host[:port][/]`, read by ADR-0007 section 4: the scheme lower-cased and never guessed, the host an IPv4 literal, a bracketed IPv6 literal or a host name, the port 0 to 65535 with leading zeros allowed, an empty or absent port the scheme's default from ADR-0007's measured default-port table, port 0 an ephemeral port, and no user, password, path, query or fragment. `0.0.0.0` and `[::]` alone bind every interface. | The URL forms `curl <url>` accepts and rejects (ADR-0007, URL and default-port tables) | curl 8.21.0 (Git for Windows), measured in ADR-0007 | Must | Draft |
| FR-005 | Several listen URLs start several listeners, in command-line order; `tftp` listens on UDP and every other scheme on TCP (ADR-0007 section 4, rules 8 and 9). | One `curl` per listen URL, each fetching from its own listener | curl 8.21.0 (Git for Windows) | Must | Draft |
| FR-006 | Once every listener has bound, surl writes `Listening on <scheme>://<host>:<bound port>/` to stdout once per listen URL, in command-line order, the bound port being the one actually bound (ADR-0007 section 7). | None directly: it tells the operator, and a test, which port upstream curl should fetch from | curl 8.21.0 (Git for Windows) | Must | Draft |
| FR-007 | The command line is read by curl's conventions as ADR-0007 section 1 decides: bundled short flags, attached or separate arguments, `--option=value`, `--`, `--no-` on negatable flags, interleaved options and URLs, last value wins, no abbreviation; with two decided differences, refusing `=value` on a flag and refusing a scheme-less listen URL. | curl's own command-line conventions (ADR-0007 rows 1 to 41) | curl 8.21.0 (Git for Windows), measured in ADR-0007 | Must | Draft |
| FR-008 | The Phase 1 options of ADR-0007 section 2 are parsed with their argument kinds, defaults and error texts: `-h`/`--help`, `-V`/`--version`, `-v`/`--verbose`, `--directory`, `--allow-uploads`, `--list-directories`, `--follow-symlinks`, `--serve-dot-files`, `--max-connections`, `--max-connections-per-address`, `--idle-timeout`, `-m`/`--max-time`, `--head-timeout`, `--max-request-head`, `--max-line`, `--max-message`, `--max-filesize`, `--tlsv1.0` to `--tlsv1.3`, `--tls-max`, `--cert`, `--key`, `--cacert`. | curl's `-h`, `-V`, `-v`, `-m`, `--max-time`, `--max-filesize`, `--tlsv1.x`, `--tls-max`, `--cert`, `--key`, `--cacert`, turned to the server's side | curl 8.21.0 (Git for Windows), measured in ADR-0007 | Must | Draft |
| FR-009 | `--help` writes ADR-0007 section 6's help text and `--version` its two lines (`surl <version> (<runtime>)`, `Protocols: <schemes>`) to stdout, each returning `Ok` (0). | `curl --help`, `curl --version` (ADR-0007 rows 39 to 41) | curl 8.21.0 (Git for Windows), measured in ADR-0007 | Must | Draft |
| FR-010 | surl returns the exit codes of ADR-0005 with ADR-0007 section 5's stderr texts: `Ok` 0 (including after Ctrl+C or SIGTERM), `UnsupportedProtocol` 1, `FailedInit` 2, `MalformedUrl` 3, `CouldNotResolveHost` 6, `CouldNotReadFile` 37, `BindFailed` 45, `InternalError` 125. | curl's `CURLE_*` numbers for the same failures (ADR-0005's measured table) | curl 8.21.0 (Git for Windows), measured in ADR-0005 | Must | Draft |
| FR-011 | By default uploads are refused, directory listings are refused, symbolic links and other reparse points are not followed, and dot-files are hidden; `--allow-uploads`, `--list-directories`, `--follow-symlinks` and `--serve-dot-files` turn each on, and a link whose target leaves the served directory is refused whatever the options (ADR-0006 section 2). A hidden entry or refused listing is answered exactly as a missing one; a refused upload gets the protocol's "not permitted" answer. | `curl <url>` for a hidden, linked or directory path; `curl -T`/`--upload-file` and `-d` uploads | curl 8.21.0 (Git for Windows) | Must | Draft |
| FR-012 | A connection past a connection limit, a head timeout, a head or line that is too long, and an upload that is too large are each answered in the protocol's own words as ADR-0006 section 5's table says (for HTTP: `503`, `408`, `431`, `413`, each with `Connection: close`), then closed gracefully; no limit ever ends the process or changes its exit code. | curl's report of each answer, e.g. `-f` exit 22 for `503` (ADR-0006, measured refusals) | curl 8.21.0 (Git for Windows), measured in ADR-0006 | Must | Draft |
| FR-013 | With `-v`, surl writes every exchange event to stderr as `#<exchange id> <marker> <text>` lines, `<` for bytes received, `>` for bytes sent and `*` for notes, split and escaped as ADR-0007 section 8 and ADR-0006 section 3 decide; without `-v` it writes none. | curl's own `-v` markers, kept relative to the printer (ADR-0007) | curl 8.21.0 (Git for Windows), measured in ADR-0007 | Should | Draft |
| FR-014 | The HTTP server issues authentication challenges and verifies the answers upstream curl sends, through `Surl.Authentication` (product overview, Phase 1 row and "Also in scope"). | `curl -u`/`--user` with `--basic`, `--digest`, `--ntlm`, `--negotiate`; `--oauth2-bearer`; `--aws-sigv4` | curl 8.21.0 (Git for Windows) | Should | Draft |
| FR-015 | The HTTP server sets cookies with every attribute upstream curl parses and records the cookies curl sends back, through `Surl.Cookies` (product overview, Phase 1 row and "Also in scope"). | `curl -b`/`--cookie`, `-c`/`--cookie-jar` | curl 8.21.0 (Git for Windows) | Should | Draft |
| FR-016 | `surl dict://<host>:<port>/` answers the DICT requests upstream curl sends (product overview, Phase 1 row). | `curl dict://<host>:<port>/<command>` | curl 8.21.0 (Git for Windows) | Should | Draft |
| FR-017 | `surl gopher://<host>:<port>/` serves the served directory over Gopher (product overview, Phase 1 row). | `curl gopher://<host>:<port>/<type><selector>` | curl 8.21.0 (Git for Windows) | Should | Draft |
| FR-018 | `surl telnet://<host>:<port>/` answers the TELNET sessions upstream curl opens (product overview, Phase 1 row). | `curl telnet://<host>:<port>` | curl 8.21.0 (Git for Windows) | Should | Draft |
| FR-019 | `surl tftp://<host>:<port>/` serves the served directory over TFTP, answering each transfer from a new port (RFC 1350; ADR-0004 section 3) (product overview, Phase 1 row). | `curl tftp://<host>:<port>/<file>`, and `-T`/`--upload-file` | curl 8.21.0 (Git for Windows) | Should | Draft |
| FR-020 | `surl mqtt://<host>:<port>/` answers MQTT 3.1.1 subscribe and publish from upstream curl (product overview, Phase 1 row; ADR-0006 section 5). | `curl mqtt://<host>:<port>/<topic>` (subscribe), and with `-d` (publish) | curl 8.21.0 (Git for Windows) | Should | Draft |
| FR-021 | A secure scheme (`https` first) is served over server-side TLS, accepting TLS 1.2 and 1.3 by default and the bounds `--tlsv1.x` and `--tls-max` set (ADR-0006 section 4), with the certificate from `--cert` and `--key` (ADR-0007 section 2; the rest is BL-002's ADR). | `curl https://<host>:<port>/<path>`, with `-k`/`--insecure`, `--cacert`, `--tlsv1.x`, `--tls-max` | curl 8.21.0 (Git for Windows), TLS versions measured in ADR-0006 | Should | Draft |

Priorities use MoSCoW (Must / Should / Could / Won't). "Must" means the release is
not shippable without it — if everything is a Must, nothing is. Phase 1 proves "`surl
http://...` serves and upstream curl fetches from it; the seams hold", so the rows that
proof needs are Must and the servers that ride alongside it are Should.

## Non-functional

Qualities rather than behaviours. Each one needs a number, or it is not a
requirement but a wish.

| ID | Quality | Target | Status |
| --- | --- | --- | --- |
| NFR-001 | Unit tests need no network (success criterion 3; root `CLAUDE.md`) | `dotnet test --filter "TestCategory!=Integration"` opens 0 sockets and is green on 3 platforms: Windows, Linux and macOS | Draft |
| NFR-002 | Line coverage of every `*.UnitLibrary` and `Surl.Console` (root `CLAUDE.md`, "Quality gates") | 100% | Draft |
| NFR-003 | Branch coverage of every `*.UnitLibrary` and `Surl.Console` | 100% | Draft |
| NFR-004 | Cyclomatic complexity per method (`CodeMetricsConfig.txt`, `CA1502`, enforced at build time) | at most 10 | Draft |
| NFR-005 | CRAP score per method | at most 30 | Draft |
| NFR-006 | It drops onto `PATH` (success criterion 4; "Constraints", "Publishing") | `dotnet publish Surl.Console` produces 1 native ahead-of-time file that runs with 0 .NET runtimes installed | Draft |
| NFR-007 | Dependencies (root `CLAUDE.md`, "Base class library only"; "Constraints", "Runtime") | 0 packages in production projects; 1 package in the solution, the MSTest meta-package, for tests | Draft |
| NFR-008 | Concurrent connections and datagram flows, all listeners together (ADR-0006 section 1) | 1024 by default, set by `--max-connections`, 0 for no limit | Draft |
| NFR-009 | Concurrent connections and datagram flows from one remote IP address (ADR-0006 section 1) | 100 by default, set by `--max-connections-per-address`, 0 for no limit | Draft |
| NFR-010 | Exchange idle timeout (ADR-0006 section 1) | 120 s by default, above libcurl's 118 s connection-cache age; set by `--idle-timeout`, 0 for no limit | Draft |
| NFR-011 | Maximum exchange duration (ADR-0006 section 1) | 3600 s by default, set by `-m`/`--max-time`, 0 for no limit | Draft |
| NFR-012 | Head timeout: time to deliver a complete request head, command line or first packet, TLS handshake included (ADR-0006 section 1) | 30 s by default, set by `--head-timeout`, 0 for no limit | Draft |
| NFR-013 | Maximum HTTP/1.x and RTSP request head (ADR-0006 section 1) | 102400 bytes by default, set by `--max-request-head`, 0 for no limit | Draft |
| NFR-014 | Maximum command line of a line-oriented protocol, line ending included (ADR-0006 section 1) | 8192 bytes by default, set by `--max-line`, 0 for no limit | Draft |
| NFR-015 | Maximum framed message of a binary protocol (ADR-0006 section 1) | 1048576 bytes by default, set by `--max-message`, 0 for no limit | Draft |
| NFR-016 | Maximum upload (ADR-0006 section 1) | 104857600 bytes by default, set by `--max-filesize`, 0 for no limit; a partial upload over it is deleted | Draft |
| NFR-017 | Write deadline for a refusal when a limit is hit (ADR-0006 section 5) | 1 s, then a graceful close; 0 connection resets for a limit | Draft |
| NFR-018 | TLS versions accepted by default (ADR-0006 section 4) | 2 versions, TLS 1.2 and TLS 1.3; SSL 2, SSL 3, TLS 1.0 and TLS 1.1 refused; client-initiated renegotiation off | Draft |
| NFR-019 | What a peer may learn (ADR-0006 section 3) | 0 local paths, exception messages or types, stack traces, operating-system error texts or user names in any byte sent to a peer; 0 version numbers in any banner | Draft |
| NFR-020 | Escaping in the verbose log (ADR-0006 section 3) | 0 bytes outside 0x20 to 0x7E written raw: every other byte, and backslash, as `\xHH`, CR and LF as `\r` and `\n` | Draft |

## Out of scope

Requirements considered and explicitly rejected, with the reason. Keeping them
here stops them being re-proposed every few months.

| Considered | Why it is out |
| --- | --- |
| A managed NuGet API | The `surl` executable is the only product; its libraries are implementation, not a published API, and no package is shipped (Stewart, 2026-09-28; product overview, "Non-goals"). |
| Validating Surl against the Curl port, another server, or a specification read apart from what curl sends | Upstream curl is the only oracle (ADR-0003; product overview, "Non-goals"). |
| Beating dedicated servers on throughput | Fidelity to what upstream curl expects is the target, not a request rate (product overview, "Non-goals"). |
| Protocols upstream curl does not request | Surl answers curl; a protocol curl cannot speak has no mate to be (product overview, "Non-goals"). |
| A `file://` listen URL | `file` has no wire and no server; the served directory is `--directory` (ADR-0002; ADR-0007 section 4). |
| Guessing `http` for a scheme-less listen URL, as curl does for a request URL | The scheme picks the protocol a public address speaks (ADR-0007, "Alternatives considered"). |
| Ignoring `=value` on a flag, as curl does | `--allow-uploads=no` would allow uploads (ADR-0007, "Alternatives considered"). |

## Open questions

Unresolved points that block requirements from leaving `Draft`. Each should name
who can answer it. None is Stewart's: every question the product overview listed has his
answer (product overview, "Open questions").

| # | Question | Blocks | Answered by |
| --- | --- | --- | --- |
| 1 | The server-side TLS contract: what a secure scheme does with no `--cert`, which formats `--cert` and `--key` read, and how a connection is upgraded in the middle of an exchange. | FR-021 | Claude, in BL-002's ADR |
