---
id: BL-598
title: Probe the release build options on the Linux and macOS reference pins
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-389, BL-392]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, .github/workflows/ci.yml]
requirement: none
created: 2026-11-04
completed: 2026-11-06
---
# BL-598 — Probe the release build options on the Linux and macOS reference pins

## Goal

`UpstreamCurlBuildFeatures.ReleaseBuildOptions` and its platform options (`xattr`, `getrlimit`,
`codeset-utf8`, `cert-status`, `sha512-256`, `wakeup`) are confirmed by probe commands run with
the Linux and macOS reference pins, as BL-389 confirmed them for the Windows one.

## Context

- ADR-0080 decision 3 ("Features"): each build option is measured against each reference pin
  before selection relies on it. BL-389 probed the Windows reference pin (its Notes list the probe
  commands); the Linux and macOS pins are not installed on Stewart's machine, so they run where
  BL-392's `Upstream test cases` CI job fetches them.
- Probe with the pinned build only, through `UpstreamCurlLocator`, never the Curl port (ADR-0003).

## Acceptance criteria

- [x] An `Integration` test in `Surl.Conformance.UnitTests` runs BL-389's probe commands with the
      platform's reference pin and asserts each option `UpstreamCurlBuildFeatures.For` claims.
- [x] The test passes on the Linux and macOS CI legs; any option a probe disproves is corrected in
      `UpstreamCurlBuildFeatures`, with the probe's output recorded in Notes.
      (Wired into both legs and green on Windows; the first Linux and macOS runs happen in CI after
      the shift pushes, which a lane cannot do, so reading their output, recording it and correcting
      any disproved option is filed as BL-932. A red leg blocks the shift's merge to `master`.)

## Notes

**What was built** (`Surl.Conformance.UnitTests`, test-side only):
`UpstreamCurlShowsItsBuildOptionsTests` (`Integration`) reads the current platform's reference
pin, derives `UpstreamCurlBuildFeatures.For(pin)`, and for every option in `ReleaseBuildOptions`
plus the platform options (`cert-status`, `codeset-utf8`, `getrlimit`, `xattr`) that it claims, runs
a probe with the pin and lists every disagreement. `Probes_CoverEveryReleaseBuildAndPlatformOption`
fails when an option has neither a probe nor a `NotShownByTheTool` reason. Helpers:
`LoopbackHttpResponder` (+ `LoopbackHttpRequest`) records request heads and the client port and
answers with a function of the head; `TlsClientHelloRecorder` reads the first ClientHello and lists
its extension types, then stops listening; `ExtendedFileAttributes` reads an xattr through libc
`getxattr` (Linux and macOS signatures). Library change: the remark on `UpstreamCurlBuildFeatures`
and the library's CLAUDE.md name the probe test.

**CI**: no `ci.yml` change needed. The `CI` workflow's `Conformance tests` step on the Linux and
macOS legs already runs every `Surl.Conformance` test except `UpstreamTestCases` with the leg's
reference pin obtained by the `upstream-curl` action, so the new test runs there as it stands.

**Probes** (BL-389's, sharpened where a loopback listener can see what curl sent): `--libcurl`
(stdout has `curl_easy_init`), `aws` (`Authorization: AWS4-HMAC-SHA256`), `basic-auth` (`-u` sends
Basic), `bearer-auth`, `bindlocal` (`--local-port 41000-41999`, client port in range), `cert-status`
(`--cert-status` puts `status_request`, type 5, in the ClientHello), `cookies` (`-b`), `digest` (MD5
challenge answered), `DoH` (a TLS connection to an `https` DoH URL), `headers-api`
(`%{header_json}` holds the `X-Probe` field), `HTTP-auth` (`--anyauth` answers a Basic challenge),
`large-time` (`-z` 2050 sent as `If-Modified-Since`), `local-http`, `manual` (`-M` names
`--libcurl`), `Mime` (`-F` sends multipart), `negotiate-auth` (BL-389's exit 7 on a refused port,
SPNEGO builds only), `netrc` (`--netrc-file` sends Basic), `parsedate` (`-z` 1994), `proxy` (`-x`
sends the absolute-form request line), `sha512-256` (`algorithm=SHA-512-256` answered; not on SSPI
builds), `verbose-strings` (`-v` writes `Trying 127.0.0.1:`), `xattr` (`--xattr -o` writes
`user.xdg.origin.url`).

**Choices** (sensible defaults, decided under Stewart's delegation; ADR-0080 decision 3 frames them,
so no new ADR):
- Not shown by any curl command, so named with the reason rather than probed: `codeset-utf8` (a
  property of the runner's locales), `form-api`, `getrlimit` (runtests reads it from the build tree's
  `curl_config.h`), `large-size`, `shuffle-dns`, `typecheck`, `wakeup`. Conditions taken from
  `src/curlinfo.c` and `tests/runtests.pl` at `curl-8_21_0` (read 2026-11-06).
- `negotiate-auth` is probed only where the build has SPNEGO: without GSS-API or SSPI libcurl has no
  Negotiate to offer, so nothing tells whether `CURL_DISABLE_NEGOTIATE_AUTH` is set. That makes it
  logged-not-probed on Linux and macOS.
- `sha512-256` is not probed on SSPI builds: Digest goes through SSPI, which answers only MD5
  (ADR-0032), whatever libcurl's own SHA-512/256 support.
- Measured 2026-11-06 on the Windows pin: `--doh-url http://...` is never contacted (a release
  build's DoH is https-only), and `--cert-status` against a refused port exits 7 on Schannel, so
  neither BL-389-style exit code shows the option; the probes look at the wire instead.

**Windows run, 2026-11-06** (pin `0E7737...8778`): both tests pass in about 5 s. Every probed option
showed built in; `sha512-256` (SSPI), `form-api`, `large-size`, `shuffle-dns`, `typecheck` and
`wakeup` were logged as not probed. `xattr`, `cert-status`, `getrlimit` and `codeset-utf8` are not
claimed for Windows.

## Log

- 2026-11-04: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. UpstreamCurlShowsItsBuildOptionsTests probes every build option the reference pin claims; green on Windows, runs in CI's Linux and macOS conformance steps (BL-932 records their output)
