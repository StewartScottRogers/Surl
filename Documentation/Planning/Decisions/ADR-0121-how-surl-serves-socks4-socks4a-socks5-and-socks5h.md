# ADR-0121 — How Surl serves SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-557. Built by BL-558 (decisions 1, 2 and 6's SOCKS4 limits), BL-559 (decisions 3, 4 and
  6), BL-560 (decision 5 over TCP), BL-561 (decision 5 on a Unix domain socket) and BL-562
  (decision 7). Follow-up filed: BL-765 (GSS-API, decision 3).
- **Amends:** [ADR-0079](ADR-0079-projects-are-grouped-in-category-folders.md) decision 1: its
  table gains `Protocols/Proxy/` (decision 1). [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md)
  decision 3: its table gains decision 1's row. [ADR-0088](ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md)
  decision 7: `-x`/`--proxy` also takes the four SOCKS schemes, and `-U`, `--proxy-user-file`
  name accounts for the SOCKS server too (decisions 4 and 5).

## Context

The product overview puts "Proxies" in scope, SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h among them:
acting as the proxy that curl's `--socks4`, `--socks4a`, `--socks5`, `--socks5-hostname`,
`--socks5-basic`, `--socks5-gssapi`, `--preproxy`, `-x`/`--proxy socks*://` and `-U`/`--proxy-user`
talk to (curl 8.21.0's manpage, https://curl.se/docs/manpage.html), over TCP and, with
`--proxy socks5h://localhost/<path>`, over a Unix domain socket. ADR-0080 decision 2 marks the
`socks4`, `socks5` and `socks5unix` servers `server-not-built`. Classified by BL-399 at tag
`curl-8_21_0` (commit `68720b4837284335b2d63cb358f8f6ce65f5bc55`), 25 cases wait on this ADR:

- `socks4` (5): 700, 702, 706, 708, 729;
- `socks5` (17): 701, 703, 707, 709, 710, 711, 712, 713, 715, 716, 717, 719, 720, 721, 728, 742,
  2055 (715 and 2055 also need `http-proxy`, ADR-0088's family);
- `socks5unix` (3): 1467, 1468, 1470 - features `proxy`, `UnixSockets`, `!win32`, command
  `--socks5 localhost%SOCKSUNIXPATH` or `--proxy socks5h://localhost%SOCKSUNIXPATH`.

The oracle is RFC 1928 (SOCKS5), RFC 1929 (username/password for SOCKS5), the SOCKS4 protocol
document ("SOCKS: A protocol for TCP proxy across firewalls", Ying-Da Lee) and its SOCKS4a
extension ("SOCKS 4A: A Simple Extension to SOCKS 4 Protocol"), and what pinned upstream curl does,
measured below. Never the Curl port (ADR-0003).

### What upstream curl 8.21.0 sends to a SOCKS proxy

Measured by BL-557 on 2026-11-05 with `Record-CurlExchange.ps1 -Raw -RawIdleMilliseconds 400`
against the Windows reference build (Git for Windows' mingw64 `curl.exe`, SHA-256 `0E773709…8778`,
`UpstreamCurlBuilds.json`), each run with `-sS`, the recorder playing the proxy with the replies
shown and then answering the tunnelled request itself with `HTTP/1.1 200 OK`, `Content-Length: 2`,
`ok`. Bytes are hexadecimal; `origin.test` was never resolved by curl in the rows that name it.

| # | curl arguments | Curl sent | Proxy replied | Exit, stderr |
| --- | --- | --- | --- | --- |
| 1 | `--socks4 127.0.0.1:<p> http://127.0.0.1:8080/a` | `04 01 1F90 7F000001 00` | `00 5A 0000 00000000` | 0; then the plain `GET /a` inside the tunnel, `Host: 127.0.0.1:8080` |
| 2 | `--proxy socks4://alice@127.0.0.1:<p> http://127.0.0.1:8080/a` | `04 01 1F90 7F000001 "alice" 00` | `00 5A …` | 0: the proxy URL's user name is SOCKS4's user ID |
| 3 | `--socks4a 127.0.0.1:<p> http://origin.test:8080/a` | `04 01 1F90 00000001 00 "origin.test" 00` | `00 5A …` | 0: 4a's `0.0.0.1` and the host name after the empty user ID |
| 4 | `--socks4 … http://127.0.0.1:8080/a` | row 1's bytes | `00 5B 0000 00000000` | 97, `curl: (97) [SOCKS] cannot complete SOCKS4 connection to 0.0.0.0:0. (91), request rejected or failed.` |
| 5 | `--socks5 127.0.0.1:<p> http://127.0.0.1:8080/a` | `05 02 00 01`, then `05 01 00 01 7F000001 1F90` | `05 00`, then `05 00 00 01 7F000001 1F90` | 0; curl offers no-authentication **and GSS-API** unasked |
| 6 | `--socks5-hostname … http://origin.test:8080/a` | `05 02 00 01`, then `05 01 00 03 0B "origin.test" 1F90` | as row 5 | 0: an ATYP 3 request answered with an ATYP 1 reply is accepted |
| 7 | `--socks5 … http://[::1]:8080/a` | `05 01 00 04 <16 bytes ::1> 1F90` | as row 5 | 0: ATYP 4 |
| 8 | `--socks5-hostname … -U alice:secret http://origin.test:8080/a` | `05 03 00 01 02`, then `01 05 "alice" 06 "secret"`, then row 6's request | `05 02`, `01 00`, then row 5's reply | 0: RFC 1929 sub-negotiation, version `01` |
| 9 | as row 8 with `-U alice:wrong` | `05 03 00 01 02`, `01 05 "alice" 05 "wrong"` | `05 02`, `01 01` | 97, `curl: (97) User was rejected by the SOCKS5 server (1 1).` |
| 10 | `--socks5 …` (and again with `--socks5-gssapi`) | `05 02 00 01` | `05 FF` | 97, `curl: (97) No authentication method was acceptable.` |
| 11 | `--socks5 …`, reply code `01`, `02`, `05`; `--socks5-hostname …`, `04` | row 5's or row 6's request | `05 <code> 00 01 00000000 0000` | 97, `curl: (97) cannot complete SOCKS5 connection to <host>. (<code>)` for each |

Every failure, SOCKS4 or SOCKS5, is curl's exit 97 (`CURLE_PROXY`); curl closed the connection
after each refusal. Curl never sent `BIND` (`02`) or `UDP ASSOCIATE` (`03`) in any run, and its
manpage has no option that asks for either: every SOCKS option names a proxy to `CONNECT` through.

### What the upstream cases ask of the server

Upstream's `socksd` is configured per case by `<servercmd>` lines (FILEFORMAT.md at the tag,
`tests/server/socksd.c`): `method 2` with `user` and `password` (717, 742: an account, 742's user
and password 255 bytes each), `backendport <port>` (717, 742: connect every request to that port),
and `response <code>` (702: `91`; 703: `1`) to send that reply instead of connecting. Without
`backendport`, `socksd` connects every request to `127.0.0.1` at the requested port, which is how
719 (`[2200::33]`), 720 (`12.34.56.78`) and 721, 1468, 1470 (`this.is.a.host.name`) reach the HTTP
server half. 716 (a 256-byte proxy user), 728 (a 256-byte redirect host for `socks5h`) and 729
(a 1015-byte SOCKS4a user ID) expect exit 97 from curl's own client-side checks.

## Decision

### 1. The library and its folder

| Horizontal library or server | May itself reference | Holds |
| --- | --- | --- |
| `Surl.Protocol.Socks.UnitLibrary`, in `Protocols/Proxy/` | `Surl.Protocol.Abstractions.UnitLibrary` | the SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h server, decisions 2 to 6 |

- **One server for both versions**, with its `.UnitTests` twin beside it, namespace
  `Surl.Protocol.Socks`. The first byte read on a connection, the version (`04` or `05`), picks
  the state machine; any other first byte closes the connection without a reply, since there is
  no version in which to answer. It is a protocol server under every ADR-0002 rule: no other
  protocol server referenced, no socket constructed, the solution's quality gates held.
- **It reaches the origin through ADR-0088 decision 2's `IOriginConnector`** (BL-551), with the
  same allowed-origin rule (loopback unless `--proxy-allow-origin` widens it), the same refusal of
  the proxy's own endpoint, and the same resolve-then-check order that defeats DNS rebinding.
  Accounts come through `IAuthenticationPolicy` and `PasswordLogin` in `Surl.Protocol.Abstractions`,
  as every other server checks passwords, so it needs no `Surl.Authentication` reference.
- **`Protocols/Proxy/` is a new category**, under ADR-0079 decision 3: a SOCKS proxy's job is
  relaying a client's connection to an origin, which none of `DataStore`, `FileTransfer`, `Http`,
  `Lookup`, `Mail`, `Messaging` or `Terminal` is for. ADR-0079 decision 1's table gains the row
  `Protocols/Proxy/` - `Surl.Protocol.Socks`; in ordinal order it sits between `Messaging` and
  `Terminal`. The folder appears with its first project (as ADR-0095 decision 3): **BL-558**
  creates it, adds the `/Protocols/Proxy/` solution folder to `Surl.slnx`, and adds `Proxy/` to
  root `CLAUDE.md`'s "Repository layout" tree (`Proxy/ ← Socks`) and to the product overview's
  list of `Protocols/` folders.
- **`Surl.Protocol.HttpProxy` stays in `Protocols/Http/`**, where ADR-0088 decision 1 puts it.
  It is an HTTP server by every byte it reads and writes, and it shares `Surl.HttpMessage`, which
  ADR-0079 decision 2 keeps beside the servers that share it; moving it would split the HTTP
  libraries across two folders for no gain, and would change BL-552's `touches` while it waits.

### 2. SOCKS4 and SOCKS4a

- **The request** is `VN=04`, `CD`, `DSTPORT` (2 bytes), `DSTIP` (4 bytes), `USERID`, `00` (the
  SOCKS4 document; rows 1 and 2). When `DSTIP` is `0.0.0.x` with `x` not zero, a host name and its
  `00` follow the user ID (the SOCKS4a document; row 3), and the server resolves it through
  `IOriginConnector`, as curl's `--socks4a` asks. `0.0.0.0` itself is an address, not 4a, and is
  refused as `NotAllowed` like any address the rule does not admit.
- **`CONNECT` only.** `CD=01` connects; `CD=02` (`BIND`) and any other code are answered `5B`.
  No curl run sends `BIND` (measured above, and no curl option asks for one), and `BIND`
  would make the proxy open an inbound listener for a remote peer - attack surface for no curl
  exchange. This is the faithful answer for curl's client, not a feature dropped for difficulty.
- **The user ID** is read (decision 6 bounds it) and written to the exchange log as
  `SOCKS4 user ID <id>` when not empty, but never checked: SOCKS4 has no password, and its
  identd check (`5C`, `5D`) asks the client's host to vouch for the user, which Surl does not
  query, so `5C` and `5D` are never sent. When the listener has proxy accounts (decision 4), every
  SOCKS4 request is answered `5B`, since it cannot prove an account, unless `--allow-anonymous`.
- **The reply** is eight bytes: `00`, `5A` (granted) or `5B` (rejected or failed), and `DSTPORT`
  and `DSTIP` as zeros: the SOCKS4 document says they are ignored for `CONNECT`, and curl
  accepted zeros (rows 1 and 4). Every refusal (`NotAllowed`, `HostNotFound`, `Refused`,
  `Unreachable`, `TimedOut`, an unsupported command, the account rule above) is `5B`, then the
  connection is closed; curl exits 97 (row 4).
- After `5A` the connection is a byte relay, decision 3's last point.

### 3. SOCKS5

- **Method selection** (RFC 1928 section 3). The server reads `VER=05`, `NMETHODS`, `METHODS` and
  selects, in this order:

  | Listener | Offered by curl | Selected |
  | --- | --- | --- |
  | no proxy accounts | `00` | `00` (no authentication) |
  | proxy accounts | `02` | `02` (username/password, decision 4) |
  | proxy accounts and `--allow-anonymous` | `00` | `00`; otherwise `02`, accepted unchecked |
  | any | none of the above | `FF`, then close (row 10; curl exits 97) |

  **GSS-API (`01`) is never selected yet.** Curl offers it on every greeting (row 5), and
  `--socks5-gssapi` alone gets `FF` (row 10). RFC 1961 GSS-API with Kerberos is in scope -
  `Surl.Kerberos` already accepts Kerberos tokens - and is sequenced to **BL-765**, which amends
  this table; it is not refused.
- **The request** (RFC 1928 section 4): `VER=05`, `CMD`, `RSV`, `ATYP`, `DST.ADDR`, `DST.PORT`.
  `ATYP 01` (IPv4, row 5), `04` (IPv6, row 7) and `03` (a length-prefixed domain name, curl's
  `socks5h`, row 6) are each connected through `IOriginConnector`; a domain name is resolved by the
  server, never by curl. `RSV` is not checked (RFC 1928 says only that it is `00`; curl sends
  `00`).
- **Replies** (RFC 1928 section 6), each `05 REP 00 ATYP BND.ADDR BND.PORT`:

  | `REP` | Sent when |
  | --- | --- |
  | `00` succeeded | the origin accepted; `BND.ADDR` and `BND.PORT` are the outbound connection's local address and port, `ATYP 01` or `04` (row 6 measured curl accepting `01` for an `03` request) |
  | `01` general SOCKS server failure | anything not below: an `IOriginConnector` failure outside `OriginConnectionFailure`, or the upstream case's `response 1` (decision 7) |
  | `02` connection not allowed by ruleset | `NotAllowed`: the allowed-origin rule, or the proxy's own endpoint |
  | `03` network unreachable | `Unreachable` |
  | `04` host unreachable | `HostNotFound`: a domain name that did not resolve, the empty name included |
  | `05` connection refused | `Refused` |
  | `06` TTL expired | `TimedOut`: the origin did not accept within `--head-timeout` (ADR-0088 decision 2) |
  | `07` command not supported | `CMD` `02` (`BIND`), `03` (`UDP ASSOCIATE`) or any other code |
  | `08` address type not supported | `ATYP` other than `01`, `03` or `04` |

  Every failure reply carries `ATYP 01` and `0.0.0.0:0` (row 11 measured curl accepting it), and
  the connection is then closed (RFC 1928 section 6: "MUST terminate the TCP connection shortly
  after sending the reply"); curl exits 97 with the code in its message.
- **`BIND` and `UDP ASSOCIATE` are answered `07`**, for decision 2's reason: curl sends neither,
  and each would open a listener or a UDP relay for a remote peer.
- **A byte relay after success**, as ADR-0088 decision 4's tunnel: both directions unchanged and
  unread (TLS inside is passed through, case 1470), a half-close passed on, both sides closed when
  both have shut or either resets; bytes either way keep the exchange from idling out.
- **A malformed exchange** - a second-stage `VER` that is not `05`, a request that ends before its
  last field - closes the connection without a reply; one that stalls ends at decision 6's
  handshake timeout.

### 4. RFC 1929 accounts

- **Named on the command line as the HTTP proxy's are** (ADR-0088 decision 5): `-U`/`--proxy-user
  <user:password>` and `--proxy-user-file <file>`, curl's own `-U` mirrored. They are the accounts
  of every `--proxy` listener in the process, HTTP or SOCKS, because curl's `-U` is one option
  whichever proxy type it talks to (row 8 sent `-U` as RFC 1929). `--proxy-auth` names HTTP
  authentication schemes and does not apply to SOCKS; its `--aihelp` facts say so.
- **The sub-negotiation** (RFC 1929 section 2): `VER=01`, `ULEN`, `UNAME`, `PLEN`, `PASSWD`, both
  1 to 255 bytes by their length byte (case 742 uses 255 each). The reply is `01 00` on success and
  `01 01` on failure, after which the connection is closed (RFC 1929 section 2); curl exits 97 with
  `User was rejected by the SOCKS5 server (1 1).` (row 9). A sub-negotiation `VER` other than `01`
  closes the connection without a reply.
- **Plain text, secure by default.** RFC 1929 sends the password in clear and SOCKS has no TLS,
  so, as ADR-0032 decides for MQTT's clear password, a login is answered `01 01` without checking
  the password unless `--allow-plaintext-auth` is given; the exchange log names the reason
  (`SOCKS5 password refused: sent in clear without --allow-plaintext-auth`). A failed check takes
  ADR-0032's fixed 1-second delay before the `01 01`. The log never holds the password.

### 5. The command line

- **The listen URL** is a SOCKS proxy URL in curl's own schemes, given positionally or with
  `-x`/`--proxy` (both mean the same; a SOCKS scheme has no other meaning, unlike a bare `http://`,
  ADR-0088 decision 7): `surl socks5://127.0.0.1:1080`, `surl --proxy socks4://127.0.0.1:1080`.
  The default port is 1080, curl's for every SOCKS scheme. A path other than empty or `/`, a user
  name or a query in the listen URL is refused `FailedInit` (2), as every listen URL refuses them.
- **The scheme says which versions the listener serves**, so it does what it says: `socks4://` and
  `socks4a://` serve SOCKS4 and SOCKS4a; `socks5://` and `socks5h://` serve SOCKS5 with all three
  address types. 4a and 5h are the client's choice of who resolves the name, which one server
  answers either way. A SOCKS5 greeting on a SOCKS4 listener is answered `05 FF`, and a SOCKS4
  request on a SOCKS5 listener `00 5B`, so curl reports a clear refusal (exit 97) instead of a
  hang. A `socks4://` and a `socks5://` listener on the same port are refused at start-up as any
  two listeners on one address are.
- **On a Unix domain socket** with ADR-0094's form: `surl --unix-socket /tmp/surl-socks.sock
  socks5h://localhost`, which curl reaches with `--proxy socks5h://localhost/tmp/surl-socks.sock`
  or `--socks5 localhost/tmp/surl-socks.sock` (cases 1467, 1468, 1470). SOCKS4 on a socket file is
  allowed too, because ADR-0094 decision 2's listener is scheme-agnostic; the socket file's mode,
  path limits and Windows support are ADR-0094 decision 5's.
- **Every option** needs its `OptionArgumentType` and `--aihelp` facts; BL-560 adds a `socks`
  help category and topic with the four schemes, its prose and an example, and BL-561 the Unix
  socket example (root `CLAUDE.md`, "`surl --aihelp`").

### 6. The limits

ADR-0006 section 1's limits, from `ExchangeLimits`, applied as ADR-0088 decision 7 applies them to
the HTTP proxy:

- **Handshake timeout: `--head-timeout`** (30 s by default), from the connection's first byte
  until the success reply is sent: greeting, RFC 1929 sub-negotiation and request together, so a
  client that trickles a SOCKS4 user ID cannot hold the connection. On expiry the connection is
  closed without a reply. The origin's connect has the same timeout (`06`, or `5B`).
- **SOCKS4 user ID: at most 255 bytes** before its `00`, and a **SOCKS4a host name: at most 255
  bytes** before its `00` - the longest SOCKS5 and RFC 1929 allow by their length bytes, so one
  bound for every name the server reads. A field that runs past it is answered `5B` and closed
  without reading further; curl itself refuses longer ones (cases 716, 728, 729).
- SOCKS5's `NMETHODS`, domain-name length, `ULEN` and `PLEN` are one byte each, so 255 is their
  bound by construction.
- `--max-connections` counts client connections (an origin connection belongs to the exchange
  that opened it); `--max-connections-per-address` counts IP peers (not a Unix socket's,
  ADR-0094 decision 6); `--idle-timeout` and `--max-time` cover the relay. Relayed bytes are not a
  body, so `--max-filesize` does not bound them, as ADR-0088 decides for a `CONNECT` tunnel.
- No `SurlExitCode` member is added.

### 7. How the upstream cases' server half plays `socks4`, `socks5` and `socks5unix`

`Surl.Conformance.UnitTests` composes `Surl.Protocol.Socks` on `Surl.Networking`'s loopback
listener at `%SOCKSPORT` (BL-547's Unix listener at `%SOCKSUNIXPATH` for `socks5unix`), in front of
the case's other server halves, as ADR-0080 decision 2 composes every server half:

- **The origin connector** is the conformance project's own `IOriginConnector`, which connects
  every request to `127.0.0.1` at the requested port, or at `backendport` when the case gives one,
  exactly as `socksd` does. That is how 719, 720, 721, 1468 and 1470's made-up hosts reach BL-391's
  HTTP server half, how 706, 707, 711, 712 and 713's FTP control and data connections reach the FTP
  server half, how 715 and 2055's `--preproxy` reaches the HTTP proxy server half (ADR-0088
  decision 8), and how 702 and 703 (`%NOLISTENPORT`) are refused. The SOCKS library needs no seam
  for it.
- **`method 2`, `user`, `password`** become a one-account policy with `--allow-plaintext-auth`'s
  behaviour, composed by the conformance project (717, 742).
- **`response <code>`** is the one part that needs the server: ADR-0080 decision 2's optional seam,
  `SocksUpstreamTestCaseReplies`, in `Surl.Protocol.Socks`, which `Surl.Console` never composes,
  answers the request with the case's reply code (`91` as SOCKS4's `5B`, `1` as SOCKS5's `01`)
  instead of connecting (702, 703).
- Any other `<servercmd>` line in a SOCKS case gives `servercmd-unsupported:<command>`.
- BL-562 does this and records the 25 cases; 715 and 2055 count once `http-proxy` is built too, and
  1467, 1468 and 1470 run on the Linux and macOS pins only (`!win32`).

## Consequences

- The tasks that act on this ADR: **BL-558** (decisions 1, 2 and 6's SOCKS4 limits; the new
  category folder, `Surl.slnx`, root `CLAUDE.md`'s "Repository layout" and the product overview's
  folder list), **BL-559** (decisions 3, 4 and 6), **BL-560** (decision 5 over TCP and the `socks`
  `--aihelp` topic), **BL-561** (decision 5 on a Unix socket) and **BL-562** (decision 7), and
  **BL-765** (GSS-API, decision 3).
- curl's SOCKS options have a server to talk to, loopback-only and login-free by default, as the
  HTTP proxy is; accounts and wider origins are named options.
- `Surl.Protocol.Abstractions` gains nothing beyond ADR-0088's `IOriginConnector`.
- No `SurlExitCode` member is added; every curl-side failure is curl's 97.

## Alternatives considered

- **Two libraries, one per version.** Rejected: they share the connector, the relay, the limits
  and the listener; the first byte tells them apart, as upstream's single `socksd` does.
- **Moving `Surl.Protocol.HttpProxy` to `Protocols/Proxy/`.** Rejected in decision 1.
- **Placing the SOCKS server in `Protocols/Http/`.** Rejected: SOCKS is not HTTP and shares none of
  that folder's libraries.
- **One listener serving every version whatever its scheme.** Rejected: `surl socks4://…` that
  also answers SOCKS5 would do something its listen URL does not say.
- **Serving `BIND` and `UDP ASSOCIATE`.** Rejected for now: no curl exchange uses them, and each
  opens a listener or relay for a remote peer. A later ADR may serve them if a client of Surl's
  needs them.
- **Accepting RFC 1929 passwords without `--allow-plaintext-auth`.** Rejected: ADR-0032 refuses a
  clear password on every other server by default.
- **Accounts separate from the HTTP proxy's.** Rejected: curl's `-U` is one option for every proxy
  type, so one set of proxy accounts mirrors it.

## Amendment 1 (2026-11-11, BL-561): the Unix socket takes a positional SOCKS listen URL only

Decided by Claude under Stewart's delegation.

Decision 5 says a SOCKS listen URL means the same given positionally or with `-x`/`--proxy`, and
that `surl --unix-socket <path> socks5h://localhost` serves SOCKS on a socket file. On a Unix
socket the two forms part: the positional form is served on the socket (any SOCKS scheme, and
`--abstract-unix-socket` likewise), while `--unix-socket` beside a `-x`/`--proxy` listen URL of any
scheme stays refused `FailedInit` (2), `option --unix-socket: cannot be used with --proxy`, as
ADR-0088 amendment 7 refuses it. Why: the socket serves exactly one listen URL, the positional form
already reaches every case curl has (1467, 1468, 1470 connect with `--proxy socks5h://localhost/<path>`
or `--socks5 localhost/<path>`, both client-side), and keeping amendment 7's single rule means one
refusal for every proxy listen URL instead of a per-scheme exception. The `socks` `--aihelp` topic
says so, with an example of the positional form.
