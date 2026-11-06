# ADR-0123 — What plays the server half of upstream `dns` test cases

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-569. Built by BL-570.
- **Amends:** [ADR-0080](ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md)
  decision 2: the `dns` row's server half is decision 1's below, and the row says built once BL-570
  lands. [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md):
  an extended server may also play the server half of an upstream test case (decision 2).

## Context

ADR-0080 decision 2 marks the `dns` server `server-not-built`: "the test DNS server `<dns>`
sections talk to". Three upstream cases wait on it (tag `curl-8_21_0`, commit
`68720b4837284335b2d63cb358f8f6ce65f5bc55`, classified by BL-399). Each has feature
`override-dns`, environment `CURL_DNS_SERVER=127.0.0.1:%DNSPORT`, and a `<reply><dns>` section:

| Case | `<server>` | `<reply><dns>` | Command | Verified |
| --- | --- | --- | --- | --- |
| 2102 | `http`, `dns` | `A: %HOSTIP`, `AAAA: %HOST6IP` | `http://examplehost.example:%HTTPPORT/%TESTNUMBER` | `<protocol>`: `GET /2102 HTTP/1.1`, `Host: examplehost.example:%HTTPPORT`, `User-Agent`, `Accept` |
| 2103 | `dns` | `A: none`, `AAAA: none` | `http://examplehost.example/%TESTNUMBER` | exit 6 |
| 2104 | `dns` | empty | three times `http://examplehost.invalid/%TESTNUMBER` | exit 6; `<verify><dns host="examplehost.invalid QTYPE A">`: `QNAME examplehost.invalid QTYPE A`, `QNAME examplehost.invalid QTYPE AAAA` (the case is named "Get three URLs with bad hostname - cache": one query per type for three transfers) |

Upstream's server is `tests/server/dnsd.c`: one UDP socket on a random port, which reads the
case's `<dns>` lines (`A: `, `AAAA: `, `HTTPS: `, `Delay-A: ` and the other delays), answers
**every** QNAME with them (flags `0x8180`, NOERROR; an address that does not parse, such as
`none`, gives an empty answer for that type), and logs each query as `QNAME <name> QTYPE <type>`
to `dnsd.input`, which runtests compares, sorted and filtered by the `host` attribute's pattern,
with `<verify><dns>` (`tests/runtests.pl` lines 1743-1762 at the tag).

### What `override-dns` is, read from upstream curl's source at the tag

- `override-dns` is not a `curl -V` feature. Runtests reads it from the test tool `curlinfo`
  (`tests/runtests.pl` line 538; `src/curlinfo.c` line 243), which prints `override-dns: ON` only
  when `CURL_MEMDEBUG` is defined and the build resolves with c-ares or `USE_FAKE_GETADDRINFO`.
  `lib/curl_setup.h` defines `CURL_MEMDEBUG` only under `DEBUGBUILD`.
- Every read of `CURL_DNS_SERVER` is debug-only: `lib/asyn-ares.c` line 729 and
  `lib/asyn-thrdd.c` line 231 sit inside `#ifdef DEBUGBUILD`, and `lib/curl_addrinfo.c`'s and
  `lib/fake_addrinfo.c`'s inside `CURL_MEMDEBUG`. `docs/tests/TEST-SUITE.md` ("DNS server") and
  `docs/libcurl/libcurl-env-dbg.md` say the same: "To make a curl build actually use the test DNS
  server requires a debug build."

### Measured: does `CURL_DNS_SERVER` move a pinned build's queries?

`Record-CurlExchange.ps1` gained `-DnsQuery` (bind a UDP port, keep every datagram curl sends to
it in `datagrams.bin`, answer nothing) and `-CurlEnvironment` (set `NAME=value` for curl's process
only) for this. Run on Windows 11, 2026-11-05, with case 2102's command and environment:

```
.\Record-CurlExchange.ps1 -Port 18053 -DnsQuery -CurlEnvironment 'CURL_DNS_SERVER=127.0.0.1:18053' -CurlArgs '-sS','--connect-timeout','3','http://examplehost.example:18080/2102' -OutDirectory <dir>
```

| Build (`UpstreamCurlBuilds.json`) | Run | `datagrams.bin` | exit, stderr |
| --- | --- | --- | --- |
| win-x64 reference, `C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778` | 2102 with `CURL_DNS_SERVER` | 0 bytes | 6, `curl: (6) Could not resolve host: examplehost.example` |
| win-x64 supplementary static, `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`, SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648` (c-ares 1.34.6, the stunnel/static-curl recipe the Linux and macOS reference pins come from) | the same, `http://` and `https://` | 0 bytes each | 6, the same message |
| win-x64 reference | control: `tftp://127.0.0.1:18053/probe` | 138 bytes (curl's RRQ and its resends) | 7 |

The control shows the recorder keeps whatever reaches the port, so the empty files mean the builds
sent no query there: they resolved through the system resolver and ignored the variable. The
reference build's `curl -V` features line, run the same day, is `alt-svc AsynchDNS brotli HSTS
HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd`:
no `Debug`, no `TrackMemory`.

The Linux and macOS reference pins - linux-x64 `/opt/upstream-curl/8.21.0/curl`, SHA-256
`153CA463957609117D21A848BE29B70691B85F9E5CC9370C7DAA037B839A4E45`, and osx-arm64
`/opt/upstream-curl/8.21.0/curl`, SHA-256
`04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF` - cannot run on this Windows
machine, so for them `override-dns` is not measured by running them. Their answer comes from the
source above and their pinned `curl -V` lines in `UpstreamCurlBuilds.json`, neither listing
`Debug` or `TrackMemory`: both are stunnel/static-curl release builds of the unpatched tag, built
without `--enable-debug`, so `DEBUGBUILD` is undefined and `override-dns` is off. The static
Windows build above, from the same recipe with the same c-ares, behaves as the source predicts.

## Decision

1. **The `dns` server half is `Surl.Extended.Protocol.DnsQuery`'s UDP server.** BL-570 composes a
   `DnsUdpProtocolServer` (BL-481) in `Surl.Conformance.UnitTests` over the datagram listener seam,
   on loopback IPv4, and builds its zones from the case's `<reply><dns>` section through a seam in
   the DNS library (planning name `DnsQueryUpstreamTestCaseReplies`), as ADR-0080 decision 2's
   per-protocol seams do. The seam behaves as `dnsd` does, because the cases depend on it:
   - Every QNAME gets the section's records: the seam builds a root zone (apex `.`, a synthesized
     SOA) whose wildcard `*.` holds an A record for `A: ` and an AAAA record for `AAAA: `
     (RFC 4592), so `examplehost.example` and `examplehost.invalid` both match.
   - An address that does not parse (`none`) or a missing line gives NOERROR with an empty answer
     for that type, as `dnsd` does, not NXDOMAIN; the seam keeps the wildcard node in being so the
     name exists.
   - Each query is logged as `QNAME <name> QTYPE <type>` (`A`, `AAAA`, `HTTPS`, as `dnsd`'s
     `type2string`) so the checker compares `<verify><dns>` as runtests does: both sides sorted,
     the log filtered by the `host` attribute as a pattern.
   - `HTTPS: ` and the `Delay-*` lines are not in any of the three cases; a section using one gives
     `servercmd-unsupported:<line name>`, BL-570's acceptance criteria's existing rule.
   The alternative, a smaller DNS answerer in `Surl.Conformance.UnitLibrary`, is rejected: that
   library references Abstractions only (ADR-0080 decision 2), and a second DNS codec would be one
   concept built twice. Surl's own server judged by RFC 1035 and RFC 3596 is exactly what the
   conformance run exists to exercise. Upstream curl as a client of an extended server is allowed
   (ADR-0083: upstream curl is the oracle everywhere); only the Curl port is held to
   `Integration` tests.
2. **`SURL_EXTENSIONS=0` does not affect the conformance run.** The variable is read by
   `Surl.Composition` when `surl` composes its protocol servers (ADR-0083); the conformance run
   constructs `DnsUdpProtocolServer` directly and never goes through that composition, as it
   constructs every other server half.
3. **`override-dns` is absent on every reference pin, so 2102, 2103 and 2104 run on none of them
   at the reference release, and the row still flips.**

   | Case | win-x64 reference (`0E773709…8778`) | linux-x64 reference (`153CA463…4E45`) | osx-arm64 reference (`04E0E69B…EEBF`) |
   | --- | --- | --- | --- |
   | 2102 | skip `feature-missing:override-dns` | skip `feature-missing:override-dns` | skip `feature-missing:override-dns` |
   | 2103 | skip `feature-missing:override-dns` | skip `feature-missing:override-dns` | skip `feature-missing:override-dns` |
   | 2104 | skip `feature-missing:override-dns` | skip `feature-missing:override-dns` | skip `feature-missing:override-dns` |

   Today the selector checks `server-not-built` before `feature-missing` (ADR-0080 decision 3's
   order), so the three skip `server-not-built:dns`. That is no longer the true reason once the
   server half exists, so BL-570 builds it and flips the `dns` row: the skip then reads
   `feature-missing:override-dns`, which names what a pin would need. The selector takes a pin's
   features from its `features` field in `UpstreamCurlBuilds.json` (`UpstreamCurlBuildPins`), a
   copy of its `curl -V` line, which never lists `override-dns`, so it gives that reason on
   every pin with no special case. The server half is still proven by BL-570's unit tests
   (the seam's A and AAAA answers over the datagram seam, the query log, `none`), and runs for
   real if a debug build is ever pinned; pinning one is Stewart's decision (root `CLAUDE.md`),
   not taken here. No build is downloaded for this ADR.
4. **`%DNSPORT` is the UDP port of decision 1's datagram listener**, bound on port 0 like every
   other server half and read back, with `CURL_DNS_SERVER` set to `127.0.0.1:<that port>` from the
   case's `<setenv>`. The `dns` row's protocol stays `http`: curl reaches `dnsd` through its
   resolver for an `http://` URL, so `http` is what a pin's protocols line must list. Case 2102's
   URL (`%HTTPPORT`, server `http`) is answered by BL-391's HTTP server half from its `<reply>`
   data, as every `http` case is; 2103 and 2104 list only `dns` and connect to no HTTP server,
   because their names resolve to no address (exit 6).

## Consequences

- **BL-570** acts on this ADR: it adds the `<dns>` seam and query log to
  `Surl.Extended.Protocol.DnsQuery` (decision 1), composes it in the conformance run with
  `%DNSPORT` and `CURL_DNS_SERVER` (decision 4), flips the `dns` row, and records 2102, 2103 and
  2104 as `feature-missing:override-dns` on each reference pin (decision 3).
- `Record-CurlExchange.ps1` has `-DnsQuery` and `-CurlEnvironment` for any later DNS measurement.
- Should a debug build of curl 8.21.0 be pinned, the three cases run against decision 1's server
  with no change to the run; a mismatch then is filed as a defect in the seam.
- `dnsd` answers with RA set and AA clear; Surl's authoritative server sets AA. c-ares and
  `getaddrinfo` accept either, and no case checks the flags, so this difference is kept.
