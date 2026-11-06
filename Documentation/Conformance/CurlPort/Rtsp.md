# Phase 7 findings: RTSP

What `CurlPortBesideUpstreamCurlOverRtspTests` in `Conformance/Surl.Conformance.UnitTests` found when it
ran every RTSP conversation of `UpstreamCurlTalksToSurlOverRtspTests` (the tool rows of ADR-0074
decision 11) through pinned upstream curl and the pinned Curl port, each against its own fresh `surl`
(ADR-0089, BL-588).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build, `UpstreamCurlBuilds.json`) |
| Surl | commit `fdb0ab43767137d4ce308750d887b2fb2b2ab625` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05 |

## Result

16 cases on Windows: 13 agreements, 3 port defects, no Surl defect, none not comparable. Upstream
curl's run is what the mirrored test asserts in all 16.

Compared, per ADR-0089 decision 4: exit code, standard output (with `-i`'s `Date:` value normalised),
the `-v` request lines `Options_MissingPathWithFieldsVerbose_Exits0AndSendsOptionsStar` asserts on,
`surl`'s `-v` notes, and the bytes each client sent on its RTSP connection, with `User-Agent:` and
Digest's `cnonce` and `response` normalised. `Options_AnyAuth_Exits0WithDigest`'s mirrored test asserts on
the `> Authorization: Digest ` standard error line, whose `cnonce` and `response` differ every run; that
line is not compared as standard error, and the same header is compared in the request bytes instead.

## The libcurl rows

The rows of `PinnedLibcurlTalksToSurlOverRtspTests` (ADR-0074 Amendment 1) are not measured here. The
port's `Libcurl` is a managed API, not a native `libcurl-4.dll`, so ADR-0089 decision 1 (c) gives them
to BL-597, whose findings go in `Libcurl.md`.

## Findings

The test reads the first column of this table (ADR-0089 decision 5): a listed case must still show its
port defect, and an unlisted case must agree.

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |
| `Options_Digest_Exits0OnOneConnection` | `UpstreamCurlTalksToSurlOverRtspTests.Options_Digest_Exits0OnOneConnection` | RTSP-1 |
| `Options_DigestWrongPassword_Exits22With401` | `UpstreamCurlTalksToSurlOverRtspTests.Options_DigestWrongPassword_Exits22With401` | RTSP-1 |
| `Options_AnyAuth_Exits0WithDigest` | `UpstreamCurlTalksToSurlOverRtspTests.Options_AnyAuth_Exits0WithDigest` | RTSP-1 |

### RTSP-1: the port does not answer an RTSP Digest challenge

- **Phase 7 tests:** the three rows above, each mirroring the test of the same name in
  `UpstreamCurlTalksToSurlOverRtspTests`.
- **Command lines** (`{surl}` is `rtsp://127.0.0.1:<port>`; `surl rtsp://… --user tester:secret`):
  - `curl -sS -m 20 -f --digest -u tester:secret -w %{num_connects} {surl}/clip.bin`
  - `curl -sS -m 20 -f --digest -u tester:wrong -w %{response_code} {surl}/clip.bin`
  - `curl -sS -m 20 -v -f --anyauth -u tester:secret {surl}/clip.bin`
- **Upstream's result:** sends `OPTIONS * RTSP/1.0` without credentials, reads `surl`'s
  `401 Unauthorized` with its `WWW-Authenticate: Digest` challenge, then sends a second `OPTIONS *` on
  the same connection with `Authorization: Digest …`. With the right password it exits 0 (`-w
  %{num_connects}` writes `1`); with the wrong one `surl` answers 401 again and it exits 22 (`401`).
- **Port's result:** sends the first `OPTIONS * RTSP/1.0` only and stops. Request bytes on connection 0:
  upstream has byte `0x4F` (`O`, the second request's `OPTIONS`) at offset 57 of the normalised bytes;
  the port's bytes end at offset 57. With the right password the port exits 22 where upstream exits 0
  (`Options_Digest_Exits0OnOneConnection`, `Options_AnyAuth_Exits0WithDigest`); with the wrong one both
  exit 22, but the port never sent the Digest request upstream sends.
- **What is right:** RFC 2326 section 14 (`Authorization`, `WWW-Authenticate`) takes RTSP's
  authentication from HTTP, so the Digest exchange of RFC 7616 (section 3.4: the client answers a 401
  challenge with an `Authorization: Digest` request) applies. curl 8.21.0's `--digest` and `--anyauth`
  (`curl.1`) do the same over RTSP: `lib/rtsp.c` builds every request through `Curl_http_output_auth`
  and follows a 401 through the HTTP authentication negotiation, re-sending on the same connection.
