# ADR-0094 — How Surl listens on a Unix domain socket

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-571. Built by BL-547 (decisions 3, 5 and 6 in `Surl.Networking` and the contract),
  BL-548 (decisions 1, 2 and 6's start-up line, for every connection-oriented scheme but FTP),
  BL-549 (decision 7's `http-unix` cases), BL-561 (SOCKS5 on a Unix socket) and BL-562 (decision
  7's `socks5unix` cases). Follow-up filed: BL-603 (FTP and FTPS, decision 2).
- **Amends:** [ADR-0004](ADR-0004-the-listener-seam-and-the-exchange-context.md): its note that a
  Unix domain socket listen URL "fits later" is acted on here, without changing
  `IListenerFactory`'s shape (decision 6). [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md)
  section 1: `--max-connections-per-address` counts IP peers only, so it does not apply to a Unix
  socket listener (decision 6).

## Context

Upstream curl connects through a Unix domain socket instead of TCP with `--unix-socket <path>`
and, on Linux, `--abstract-unix-socket <name>` (curl 8.21.0 manual,
https://curl.se/docs/manpage.html). The URL still names the scheme and the `Host` curl sends; only
the transport changes. A SOCKS5 proxy can also be reached over a Unix socket with
`--proxy socks5h://localhost/<path>`.

Upstream test cases that wait on this (tag `curl-8_21_0`, commit
`68720b4837284335b2d63cb358f8f6ce65f5bc55`, classified with ADR-0080's rules by BL-399):

| Case | Server | Features | Command |
| --- | --- | --- | --- |
| 1435 | `http-unix` | `UnixSockets` | `--unix-socket %HTTPUNIXPATH` |
| 1436 | `http-unix` | `UnixSockets` | `--unix-socket %HTTPUNIXPATH`, several connections |
| 1467 | `socks5unix` | `proxy`, `UnixSockets`, `!win32` | `--socks5 localhost%SOCKSUNIXPATH` |
| 1468 | `socks5unix` | `proxy`, `UnixSockets`, `!win32` | `--proxy socks5h://localhost%SOCKSUNIXPATH` |
| 1470 | `socks5unix` | `proxy`, `UnixSockets`, `!win32` | HTTPS, `--proxy socks5h://localhost%SOCKSUNIXPATH` |

Every reference pin in `UpstreamCurlBuilds.json` reports `UnixSockets` in its `features` line:
the Windows reference build (Git for Windows' mingw64 curl 8.21.0, Schannel) as well as the Linux
and macOS static builds of ADR-0016.

### Measured on Stewart's Windows machine

`Record-CurlExchange.ps1` gained `-UnixSocket <path>` (and `-UnixSocketAbstract`) for this ADR: it
serves its canned HTTP responses on an `AF_UNIX` stream socket bound with the BCL's
`UnixDomainSocketEndPoint`. Against the Windows reference build (SHA-256 `0E773709…8778`) on
Windows 11 Pro 10.0.26300, .NET 10, 2026-11-04:

| Case | curl arguments | Result |
| --- | --- | --- |
| HTTP over a socket file | `-sS -v --unix-socket <temp>\bl571.sock http://example.test/a` | exit 0; the request bytes are TCP's: `GET /a HTTP/1.1`, `Host: example.test`, `User-Agent: curl/8.21.0`, `Accept: */*` |
| HTTPS over a socket file | `-sS -k --unix-socket <path> https://example.test/` with `-Tls` | exit 0, the same request inside TLS |
| FTP over a socket file | `-sS --unix-socket <path> ftp://example.test/` against the HTTP responder | curl connects through the socket and waits for a greeting (exit 56 when none comes): the option is scheme-agnostic |
| No such file | `--unix-socket <temp>\nope.sock` | exit 7, `curl: (7) Failed to connect to example.test:80 over unix://<path> after 77 ms: Could not connect to server` |
| 107-character path, no listener | `--unix-socket Z:\temp\yyy…` (107) | exit 7: accepted, nothing listening |
| 108-character path | `--unix-socket Z:\temp\yyy…` (108) | exit 6, `curl: (6) Unix socket path too long: '<path>'` |
| Abstract name, no listener | `--abstract-unix-socket surl-test` | exit 7, `Failed to connect to example.test:80 over unix://surl-test` |
| Abstract name, .NET bound `"\0surl-abstract"` | `--abstract-unix-socket surl-abstract` | exit 7: Windows' `bind` accepts the leading NUL, but curl cannot reach it, so Windows has no working abstract names |

And the BCL on the same machine: `UnixDomainSocketEndPoint` refuses a path of 108 characters or
more (`ArgumentOutOfRangeException`) and binds 107; a second bind of a path in use, and a bind of a
path where a regular file stands, fail `SocketError.AddressAlreadyInUse`; a bind in a directory that
does not exist fails (`SocketError.NetworkDown` on Windows); disposing the bound listening socket
removed its socket file.

The limits on Linux and macOS are from their `sockaddr_un`, not measured here: `sun_path` is 108
bytes on Linux and 104 on macOS, and curl refuses a path that does not fit with its terminating NUL.
BL-547's tests confirm them on CI's Linux and macOS legs.

## Decision

### 1. The command line

- `--unix-socket <path>` makes `surl` listen on a Unix domain socket file at `<path>` instead of a
  TCP port, mirroring curl's option name and argument. The listen URL is still given and still
  names the scheme, and its host and port are what the server expects in the requests it reads
  (the `Host` field curl sends: `http://localhost/` expects `localhost`); nothing is bound on
  them.
- `--abstract-unix-socket <name>` does the same with a Linux abstract socket name: the name
  without its leading NUL, as curl takes it.
- As with curl, the two set one value and the last one given wins.
- With either option, exactly one listen URL is allowed, given positionally or as `-x`/`--proxy`
  (ADR-0088 decision 7): a socket has one path and one server. A second listen URL is refused
  with `SurlExitCode.FailedInit` (2), `option --unix-socket: serves one listen URL, and <n> were
  given`, followed by the usual "try --help" line. To serve TCP and a Unix socket at once, run two
  `surl` processes.
- The path is read with `OptionArgumentReader.Path` and used as given, relative paths against the
  working directory, as curl uses its argument; an empty argument is refused `FailedInit` (2) as
  that reader refuses every blank path.
- Each option needs its `OptionArgumentType` and its `--aihelp` facts, on the `surl` category's
  page, the tool's own options (root `CLAUDE.md`, "`surl --aihelp`"). BL-548 adds them.

### 2. Which servers may listen on a Unix socket

- Every connection-oriented server, the implicit-TLS schemes included (HTTPS over a socket file is
  measured above), and the `-x` proxy servers. The listener is scheme-agnostic: a server receives
  an `IConnection` as it does from TCP and does not know the difference.
- A scheme whose server listens on datagrams (an `IDatagramProtocolServer`: `tftp` today, and the
  extended `coap` and `coaps` when they are built) is refused with `SurlExitCode.FailedInit` (2),
  `option --unix-socket: <scheme> listens on UDP`, before anything is bound.
- Options that would add a UDP listener beside it are refused the same way, by the task that
  builds each option: ADR-0082's `--http3`, and an ADR-0092 `--dns-transport` list naming `udp`
  or `quic` (`--dns-transport tcp,tls,https` is allowed). Neither option exists today.
- FTP and FTPS: the control connection may come over the socket, but its data connections are TCP
  connections to the address a `PASV` or `EPSV` reply names. BL-548 refuses `ftp` and `ftps` with
  `FailedInit` (2) until BL-603 measures what curl does and serves them, with the passive data
  listener on loopback.

### 3. Abstract socket names

- Linux only. On Windows and macOS `--abstract-unix-socket` fails with
  `SurlExitCode.BindFailed` (45), new `ListenerBindFailure.AbstractUnixSocketUnsupported`,
  `Abstract Unix socket names exist only on Linux`, before any socket is made. Windows' `bind`
  accepts the name, but curl cannot connect to it (measured above), so binding it would produce
  a listener nobody reaches; macOS has no abstract namespace at all.
- BL-571's planning assumption of `UnsupportedProtocol` (1) is not taken: the scheme is supported,
  and this is a failure to bind the requested address on this platform, the same class as an
  address that is not available, which is `BindFailed` (45).
- An abstract name has no file, so decision 5 does not apply to it; its limit is 107 bytes
  (`sun_path` less the leading NUL).

### 4. Windows `AF_UNIX`

Supported. Windows 10 1803 and later support `AF_UNIX` stream sockets, the Windows reference
build reports `UnixSockets`, and it completes exchanges over one (measured above). Surl's supported
Windows versions are all past 1803, so no version check is made; a bind refused by the OS is
decision 5's `BindFailed` (45) like any other.

### 5. The socket file

- **Created** by `surl` when it binds. On Linux and macOS its mode is set to `0600` (owner read and
  write only) between `bind` and `listen`, so no connection is accepted before it is
  owner-only: secure by default, and curl run by the same user connects. On Windows the file
  takes its directory's ACL. No option loosens it yet; one would be its own task and ADR.
- **Deleted** by `surl` when the listener stops (shutdown, SIGTERM, or failed start-up after the
  bind), and only the file it created: if the path no longer names a socket, or names another
  one, it is left alone. A crash leaves the file behind.
- **A path that exists already** is refused with `SurlExitCode.BindFailed` (45),
  `ListenerBindFailure.AddressInUse`, `Could not bind <path>: the path exists already`, whatever
  stands there - a stale socket file, a live one, a regular file, a directory. `surl` never
  deletes a file it did not create; the user removes a stale one.
- **A missing directory**, or a directory `surl` may not write, is refused `BindFailed` (45) with
  `ListenerBindFailure.AddressNotAvailable` or `PermissionDenied`, as the OS reports it.
- **The longest path** is the platform's `sun_path` less its terminating NUL, counted in UTF-8
  bytes: 107 on Linux, 103 on macOS, 107 on Windows (measured, for curl and the BCL alike). A
  longer path is refused before binding with `SurlExitCode.BindFailed` (45), new
  `ListenerBindFailure.UnixSocketPathTooLong`, `Unix socket path too long: '<path>' is <n> bytes
  and this platform allows <limit>`. Curl itself refuses the same path with exit 6, so no client
  could ever reach it.

### 6. The listen request, the start-up line, the exchange log and the peer

- **The contract.** `ListenUrl` gains an init-only `UnixSocket` of a new record
  `UnixSocketAddress(string Name, bool IsAbstract)` in `Surl.Protocol.Abstractions`; when it is
  set, `IListenerFactory.StartConnectionListenerAsync` binds it instead of host and port, and
  `BoundPort` stays unset. `IListenerFactory`'s methods do not change. Only `Surl.Networking`
  constructs the `Socket` and the `UnixDomainSocketEndPoint`.
- **The start-up line** names the URL as given and the socket in curl's own words
  (`over unix://<path>`, as curl's error does above):
  `Listening on http://localhost/ over unix:///tmp/surl.sock`, and for an abstract name
  `over unix://@surl-test` - the `@` is how `ss` and `netstat` show the leading NUL, which cannot
  be printed.
- **The local endpoint** of each exchange is the listener's `UnixDomainSocketEndPoint`.
- **The peer.** A client that connects without binding a name - curl never binds one - has no
  address. `Surl.Networking` reports it as a new `UnnamedUnixSocketPeer : EndPoint` in
  `Surl.Protocol.Abstractions` (`AddressFamily.Unix`, one shared instance), whose `ToString()` is
  `unix peer`; a client that did bind a name is its `UnixDomainSocketEndPoint`. Code that needs an
  IP address already tests for `IPEndPoint` and finds none, so the exchange log's lines read
  `... from unix peer`.
- **Limits.** Every ADR-0006 limit applies but `--max-connections-per-address`, which counts IP
  addresses (`ConnectionAdmission` skips a non-IP peer); `--max-connections` still caps the
  listener. A Unix socket's clients are local users the file's mode already admits.

### 7. The upstream cases and where they run

- 1435 and 1436 (`http-unix`) run on each platform's reference build, the three of which report
  `UnixSockets`: `linux-x64` and `osx-arm64` in CI, and `win-x64` from a local run (ADR-0080).
  BL-549 plays them, with BL-391's HTTP server half on a socket file named for
  `%HTTPUNIXPATH`, kept within decision 5's limit.
- 1467, 1468 and 1470 (`socks5unix`) carry `!win32`, so they run on `linux-x64` and `osx-arm64`
  only, and are skipped on Windows as the case says. BL-562 plays them, with BL-557's SOCKS server
  on a socket file named for `%SOCKSUNIXPATH`.

## Consequences

- The tasks that act on this ADR: **BL-547** (the Unix socket listener in `Surl.Networking`,
  `UnixSocketAddress`, `UnnamedUnixSocketPeer`, the two new `ListenerBindFailure` members, the
  file's mode and deletion), **BL-548** (`--unix-socket`, `--abstract-unix-socket`, decision 2's
  refusals and the start-up line, served by `surl`), **BL-549** (the `http-unix` cases),
  **BL-561** (SOCKS5 on a Unix socket, under BL-557's ADR) and **BL-562** (the `socks5unix`
  cases). BL-603 serves FTP and FTPS.
- No `SurlExitCode` member is added: every failure is `FailedInit` (2) or `BindFailed` (45).
  `ListenerBindFailure` gains two members, which BL-547 maps to `BindFailed`.
- `Record-CurlExchange.ps1` can measure curl over a Unix socket (`-UnixSocket`,
  `-UnixSocketAbstract`, PowerShell 7 only), for HTTP and HTTPS responses.
- Tests that bind a socket file run on all three platforms, with temporary paths short enough for
  macOS's 103 bytes; abstract-name tests run on Linux only
  (`[OSCondition(OperatingSystems.Linux)]`), and their refusal on the others
  (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Linux)]`).

## Alternatives considered

- **A listen URL that carries the path**, as curl's SOCKS proxy URL does
  (`http://localhost/tmp/surl.sock`, or a `unix:` scheme). Refused: listen URLs refuse a path
  today, curl's own option for the main URL is `--unix-socket`, and a path in the URL would be
  ambiguous with the URL's own path for the servers that read one.
- **Deleting a stale socket file at start-up**, as some servers do. Refused: a stale file and a
  live server's file look alike, and silently deleting a live server's file would orphan it.
- **`--unix-socket` applying to every listen URL**, as curl applies it to every URL it fetches.
  Refused: a server cannot bind one path twice.
- **Refusing Windows**: rejected by measurement; the reference build and the BCL both work there.
- **Leaving the file mode to the umask**: rejected as not secure by default; a socket file
  readable by others lets any local user reach the server.
