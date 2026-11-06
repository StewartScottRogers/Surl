# Phase 7 findings: the libcurl rows

What the libcurl rows of Phase 7 (ADR-0089 decision 1 (c)) found when each step list of a pinned
upstream libcurl test ran through upstream's driver, calling the pinned `libcurl-4.dll`, and through the
Curl port's twin of that driver, calling the port's pinned managed `Libcurl`, each against its own fresh
`surl` (BL-597, BL-936, BL-937). The port's output is the thing measured, never an expected value (ADR-0003
decision 6).

| Family | Phase 7 test class | Mirrors | Upstream driver | Port twin |
| --- | --- | --- | --- | --- |
| WebSocket | `CurlPortLibcurlBesideUpstreamLibcurlOverWebSocketTests` | `PinnedLibcurlTalksToSurlOverWebSocketTests` | `Run-LibcurlWebSocketScript.cs` | `Run-CurlPortLibcurlWebSocketScript.cs` |
| RTSP | `CurlPortLibcurlBesideUpstreamLibcurlOverRtspTests` | `PinnedLibcurlTalksToSurlOverRtspTests` | `Run-LibcurlRtspScript.cs` | `Run-CurlPortLibcurlRtspScript.cs` |

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
| Surl | commit `dc8aebe3c0b8dd07287f3af1983151b711a5f953` for the WebSocket rows, `2006b0770c8c905bd36f4db799c7fcdd7dd3e0e2` for the RTSP rows |
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

13 rows on Windows: 12 agreements, 1 port defect (finding 1 below), no Surl defect, none not comparable.

Each row is one step list of `PinnedLibcurlTalksToSurlOverRtspTests` - DESCRIBE; SETUP over interleaved
TCP; SETUP, PLAY and three RECEIVEs; PAUSE, GET_PARAMETER and TEARDOWN after PLAY; OPTIONS and SETUP after
TEARDOWN; SETUP over UDP (461); PLAY on a made-up session (454); RECORD on a session set up to play (455);
RECORD with no login (401); GET_PARAMETER with a body (451); ANNOUNCE with uploads off (403) and on; and
SETUP, RECORD and TEARDOWN storing an empty recording - run with a 3-second timeout against
`surl rtsp://...` with the mirrored test's options, serving its 100-byte `clip.bin`. Compared, per
ADR-0089 decision 4: the driver's exit code, its report line by line (one line per setopt and per
request, with the status, session, CSeq and interleaved bytes `libcurl` gave back), `surl`'s `-v` notes,
and the files `surl` stored (`rec.bin.sdp` from ANNOUNCE, `rec.bin` from RECORD). Session IDs, ports and
the random RTP timestamp, SSRC and RTCP sender-report fields are normalised before comparison. The
mirrored test's own assertions are checked on upstream's run.

## Findings

The tests read the first column of this table (ADR-0089 decision 5): a listed row must still show its
port defect, and an unlisted row must agree. A finding gives the row, the test it mirrors, and what the
port did beside what upstream libcurl did (decision 6).

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |
| `PlayThenReceive_ServedFile_DeliversItsBytesThenAnRtcpBye` | `PinnedLibcurlTalksToSurlOverRtspTests.PlayThenReceive_ServedFile_DeliversItsBytesThenAnRtcpBye` | [1](#1-the-ports-rtsp-receive-forgets-the-session-and-does-not-wait-for-interleaved-data) |

The Curl port's `Libcurl` agrees with upstream libcurl on every WebSocket row and on the other 12 RTSP
rows.

### 1. The port's RTSP RECEIVE forgets the session and does not wait for interleaved data

- **Command line:** `Run-LibcurlRtspScript.cs --timeout 3000 rtsp://{surl}/clip.bin
  stream-uri:rtsp://{surl}/clip.bin transport:RTP/AVP/TCP;interleaved=0-1 SETUP PLAY RECEIVE RECEIVE RECEIVE`
  (the port's twin `Run-CurlPortLibcurlRtspScript.cs` with the same arguments), against `surl rtsp://...`
  serving the 100-byte `clip.bin`. Measured 2026-11-06, port `b42c319b…`, Surl `2006b077`; the lost
  BL-597 run of the same day saw the same first difference.
- **Exit code:** both 0. Neither run stored a file.
- **First differing report line (line 5, the first `RECEIVE` after `PLAY`):** upstream
  `RECEIVE: CURLcode 0 (No error), status 0, session "<session>", cseq received 2, next cseq 3, body 0 bytes "", interleaved 1 calls: 116 bytes "$\x00\x00p…"`
  (the RTP frame alone, through one interleave callback, with the session ID and CSeq counters of the
  SETUP and PLAY still readable); port
  `RECEIVE: CURLcode 0 (No error), status 0, session none, cseq received 0, next cseq 0, body 0 bytes "", interleaved 2 calls: 116 bytes "$\x00\x00p…", 40 bytes "$\x01\x00$\x80\xC8…"`
  (both the RTP frame and the RTCP sender report and BYE in one `RECEIVE`, with
  `CURLINFO_RTSP_SESSION_ID`, `CURLINFO_RTSP_CSEQ_RECV` and `CURLINFO_RTSP_CLIENT_CSEQ` reset to nothing).
- **Later report lines:** upstream's second `RECEIVE` delivers the 40-byte RTCP frame and its third
  ends `CURLcode 28 (Timeout was reached)` with nothing interleaved; the port's second and third
  `RECEIVE`s return `CURLcode 0` at once with nothing interleaved, and still `session none`.
- **First differing surl note (line 2):** upstream's `surl` sees one connection; the port's sees
  `#2 * Exchange 2 opened: rtsp from 127.0.0.1:<port>.` and then an Exchange 3, so the port's `RECEIVE`s
  open new connections instead of reading the RTSP connection the session lives on.
- **Right behaviour:** libcurl 8.21.0's `CURLOPT_RTSP_REQUEST`
  (https://curl.se/libcurl/c/CURLOPT_RTSP_REQUEST.html) defines `CURL_RTSPREQ_RECEIVE` as reading the
  interleaved RTP data the server sends on the existing RTSP connection, handed to
  `CURLOPT_INTERLEAVEFUNCTION`; RFC 2326 section 10.12 has that interleaved binary data travel on the
  RTSP TCP connection itself. `CURLINFO_RTSP_SESSION_ID`, `CURLINFO_RTSP_CSEQ_RECV` and
  `CURLINFO_RTSP_CLIENT_CSEQ` (https://curl.se/libcurl/c/CURLINFO_RTSP_SESSION_ID.html and its
  siblings) keep the handle's session and counters across requests, and a `RECEIVE` with nothing to
  read waits until `CURLOPT_TIMEOUT` and fails with `CURLE_OPERATION_TIMEDOUT` (28), as upstream does.
  Upstream is right by ADR-0003 decision 6.
