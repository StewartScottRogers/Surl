---
id: BL-389
title: Select which upstream test cases a pinned build can run against surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-388]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-389 — Select which upstream test cases a pinned build can run against surl

## Goal

Given a parsed upstream test case and a pinned build's `UpstreamCurlBuildPins` entry,
`Surl.Conformance.UnitLibrary` decides whether the case can run against surl. If it cannot, it gives
one named skip reason from BL-386's ADR decision 3.

## Context

- Decision: BL-386's ADR, decision 3: the classes out of scope and their reasons.
- Inputs: the case's `<server>` list, `<features>` (including negated `!feature` entries, as
  FILEFORMAT.md describes), `<tool>`, `<command type=...>`, `<precheck>`, `<postcheck>` and
  `<keywords>`, plus upstream's `tests/data/DISABLED` list. The other input is the build's `protocols`
  and `features` lines from `UpstreamCurlBuilds.json`, read by `UpstreamCurlBuildPins`.
- Which servers surl can be today follows from the registered protocol servers and the ADR's
  decision 2. Proxy and SOCKS servers are not built yet.
- FILEFORMAT.md's `<features>` list (tag `curl-8_21_0`, lines 473 to 560) names features that a curl
  binary's `--version` does not show, such as `debug`, `TrackMemory` and `unittest`. Each of them is
  either a skip reason or derived from the pin. Choose per the ADR.

## Acceptance criteria

- [x] Named tests cover each skip reason with an inline case, and a runnable case for each pinned
      build's platform.
- [x] A test over every imported case and each of the three reference pins writes the count per skip
      reason and the runnable count to the test log. Notes record the counts.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

**What was built** (`Surl.Conformance.UnitLibrary`): `UpstreamTestCaseServers` (decision 2's
table, `UpstreamTestCaseServer` rows), `UpstreamCurlBuildFeatures.For(pin)`,
`UpstreamTestCaseDisabledList.Parse`, `UpstreamTestCaseSkipReasons`,
`UpstreamTestCaseSelectionRules` (`ForBuild`) and `UpstreamTestCaseSelector.FindSkipReason`.
`PinnedUpstreamCurlBuild` gains `Features` (an init property, so no constructor call changed),
read from the pin's `features` line. Tests: `UpstreamTestCaseSelectorTests` (every reason inline,
plus an `https` case runnable on each reference pin's platform), `UpstreamCurlBuildFeaturesTests`,
`UpstreamTestCaseDisabledListTests`, `UpstreamTestCaseServersTests`, a features test in
`UpstreamCurlBuildPinsTests`, and the corpus count in `UpstreamTestCaseCorpusTests`. Library
coverage measured with the MSTest coverage collector: 100% line, 100% branch.

**Choices made under ADR-0080** (decided by Claude under Stewart's delegation; the ADR frames them,
so no new ADR):
- Feature derivation follows runtests' `checksystemfeatures` at `curl-8_21_0` (read 2026-11-04):
  only the `Features:` words runtests reads become features (so `AsynchDNS`, `HTTP2` themselves do
  not; `HTTP2` becomes `http/2`), the version line's backend words (`Schannel`/OpenSSL family ->
  `SSLpinning`, `nghttp2` -> `h2c`, `libssh2` -> `sshkeyalgo`), `win32` from the tool part,
  `threaded-resolver` when `AsynchDNS` and (no c-ares or `asyn-rr`), protocols with `-ipv6`/`-unix`,
  `http-proxy`, `https-mtls`, `none`, `https-proxy`, `httptls` (TLS-SRP with https).
- `curlinfo` options (`src/curlinfo.c` at the tag): a default release build's set is
  `ReleaseBuildOptions`; `resolv-threaded` follows `threaded-resolver`, `ssl-sessions` follows
  `SSLS-EXPORT`, `cert-status` OpenSSL/GnuTLS only, `win32-ca-searchpath` on Windows only,
  `xattr` and `getrlimit` off Windows only. `codeset-utf8` (runtests asks whether `C.UTF-8` exists
  on the runner) is taken as present on Linux and macOS and absent on Windows. `crypto` is NTLM or
  Kerberos or SPNEGO. `unittest`, `Debug`, `TrackMemory`, `torture`, `nghttpx` are never present.
- A negated feature the build has is `feature-missing:!<name>` (the reason keeps the `!`).
- `needs-perl` matches `%PERL`, a `.pl` script name, or `%SRCDIR/` not followed by `data/` or
  `certs/`, in the client's `<command>` and `<file*>` parts; so a case is parsed for selection
  without `SRCDIR`/`PERL`.
- A `<servercmd>` command is judged by the first listed server's half; strip lines are every
  non-blank, non-`#` line of a verify part named `strip*`. `ForBuild` accepts no server command and
  no strip line yet (nothing implements them); BL-390 to BL-397 replace those predicates.
- `<keywords>` decide nothing: runtests skips by keyword only when told to on its command line.
- Port variables per server name are left to the command-building task; the table holds scheme and
  built-ness only.
- Production regexes are `static readonly Regex` fields, not `[GeneratedRegex]`, whose generated
  code would count against the library's coverage.

**Probes, Windows reference pin** (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E7737...8778` verified, 2026-11-04; all loopback, no server): `-M` printed the manual (manual);
`--libcurl - <file url>` printed the generated C (--libcurl); `-w %{header_json}` printed JSON
(headers-api); `-v` printed verbose text (verbose-strings); `-x http://127.0.0.1:1`, `--digest`,
`--aws-sigv4`, `-F`, `--negotiate`, `--oauth2-bearer`, `-b` against `http://127.0.0.1:1/` each
exited 7 (connect refused, so the option is built in: proxy, digest, aws, Mime, negotiate-auth,
bearer-auth, cookies); `--doh-url http://127.0.0.1:1/` exited 6 (DoH tried); `--netrc-file`
and `-z <date>` were accepted (netrc, parsedate). The Linux and macOS pins are not installed
here; filed BL-594 to probe them in CI.

**Counts** (`UpstreamTestCaseCorpusTests.FindSkipReason_EveryImportedCaseForAReferencePin_...`,
2,013 cases; "today" = `ForBuild`, "all" = every server command and strip line accepted):

| Reason | win-x64 | linux-x64 | osx-arm64 |
| --- | --- | --- | --- |
| runnable today / all | 855 / 1,167 | 945 / 1,288 | 945 / 1,288 |
| unparsable (758, 1569, 1570, 1614) | 4 | 4 | 4 |
| disabled-upstream | 19 | 14 | 14 |
| unittest | 81 | 81 | 81 |
| libtest | 349 | 350 | 350 |
| debug-build | 50 | 50 | 50 |
| perl-command | 27 | 27 | 27 |
| precheck | 19 | 19 | 19 |
| postcheck | 14 | 14 | 14 |
| no-server-half | 87 | 87 | 87 |
| server-not-built (http-proxy 24, socks5 15, https-proxy 5, socks4 5, http/2 4, httptls+srp 4, dns 3, socks5unix 3, http/3 2, http-unix 2) | 67 | 67 | 67 |
| protocol-missing:smb | 2 | 0 | 0 |
| feature-missing | 127 (!SSPI 86, !win32 18, !Schannel 10, !IDN 3, ECH 2, GSS-API 2, h2c 2, !IPv6 1, OpenSSL 1, http/2 1, ssl-sessions 1) | 12 (Schannel 6, !IDN 3, GSS-API 2, !IPv6 1) | 12 (same) |
| servercmd-unsupported (today; REPLY 80, AUTH 62, CAPA 10 on Windows ...) | 199 | 220 | 220 |
| strippart-unsupported (today) | 113 | 121 | 121 |

Each "today" column totals 2,013. No case names an `rtsp` server, so macOS's missing `rtsp`
removes none. The ADR's own pre-feature classification (82 unittest, 352 libtest) differs by a few
because selection checks `DISABLED` first and parses each case with the pin's features.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. UpstreamTestCaseSelector gives each upstream test case one ADR-0080 skip reason or runnable for a reference pin; counts per pin logged
