# Phase 7 findings: the libcurl rows

What the libcurl rows of Phase 7 (ADR-0089 decision 1 (c)) found when each step list of a pinned
upstream libcurl test ran through upstream's driver, calling the pinned `libcurl-4.dll`, and through the
Curl port's twin of that driver, calling the port's pinned managed `Libcurl`, each against its own fresh
`surl` (BL-597, BL-936). The port's output is the thing measured, never an expected value (ADR-0003
decision 6).

| Family | Phase 7 test class | Mirrors | Upstream driver | Port twin |
| --- | --- | --- | --- | --- |
| WebSocket | `CurlPortLibcurlBesideUpstreamLibcurlOverWebSocketTests` | `PinnedLibcurlTalksToSurlOverWebSocketTests` | `Run-LibcurlWebSocketScript.cs` | `Run-CurlPortLibcurlWebSocketScript.cs` |
| RTSP | not yet written (the next task after BL-936) | `PinnedLibcurlTalksToSurlOverRtspTests` | `Run-LibcurlRtspScript.cs` | `Run-CurlPortLibcurlRtspScript.cs` |

The twins take the upstream drivers' command line and print the same one-line-per-call report. Each
compiles against the `Libcurl.dll` that `SURL_CURL_PORT_LIBCURL` names through
`CurlPortLibcurlReference.targets`, and checks that file's SHA-256 against `CurlPortBuilds.json`'s
`kind: "library"` pin before loading any of its types. Exit codes are the upstream drivers': 0 every step
ran, 1 perform failed, 2 malformed command line, 3 not pinned, 4 Inconclusive.

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `Libcurl.dll` SHA-256 `613976A7CC3DA746AB74819C6A526D676A39998D56B5470D5C527A902D7C8311` (`CurlPortBuilds.json`, `win-x64`, kind `library`) |
| Upstream libcurl | `libcurl-4.dll` SHA-256 `799F7EEFC3C9DA9C80EC5AEA221A02B3AFE2C5350C6B45FD5A4865E7E2D4E574`, `libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference library, `UpstreamCurlBuilds.json`) |
| Surl | commit `dc8aebe3c0b8dd07287f3af1983151b711a5f953` |
| Platform | Windows (`win-x64`); off Windows every row is Inconclusive, since only the Windows reference build ships a shared libcurl and the port's library is pinned only for `win-x64` (ADR-0071 decision 10, ADR-0089 decision 8) |
| Date | 2026-11-06 |

## Result

### WebSocket

7 rows on Windows: 7 agreements, no port defect, no Surl defect, none not comparable.

Each row is one step list of `PinnedLibcurlTalksToSurlOverWebSocketTests` - text, binary, three
fragments, ping, close 1000, a 2 MiB message closed with 1009, and invalid UTF-8 closed with 1007 - run
against `surl ws://... --ws-echo` started in memory. Compared, per ADR-0089 decision 4: the driver's
exit code, its report line by line (the perform line and one line per send and receive), and `surl`'s
`-v` notes. The mirrored test's own assertions are checked on upstream's run.

### RTSP

Not measured yet: the twin `Run-CurlPortLibcurlRtspScript.cs` exists, and the Phase 7 test class that
runs `PinnedLibcurlTalksToSurlOverRtspTests`' step lists through it is the next task's.

## Findings

The tests read the first column of this table (ADR-0089 decision 5): a listed row must still show its
port defect, and an unlisted row must agree. A finding gives the row, the test it mirrors, and what the
port did beside what upstream libcurl did (decision 6).

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |

None: the Curl port's `Libcurl` agrees with upstream libcurl on every WebSocket row.
