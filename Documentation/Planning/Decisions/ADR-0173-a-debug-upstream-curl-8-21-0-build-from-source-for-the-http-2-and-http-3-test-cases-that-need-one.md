# ADR-0173 — A Debug upstream curl 8.21.0 build, built from source, for the http/2 and http/3 test cases that need one

- **Status:** Accepted
- **Date:** 2026-11-16
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-16, in
  BL-1303. Stewart approved the download and build on 2026-11-16, answering "pinning a Debug build
  is a download of upstream curl, do you want one?": **"download"**. That approval covers the
  `curl-8_21_0` sources of https://github.com/curl/curl and the library sources this build links;
  any other upstream version, or any further download, still needs his approval.
- **Applies:** [ADR-0076](ADR-0076-an-openldap-upstream-curl-8-21-0-build-built-reproducibly-in-ci-for-ldap-and-ldaps.md)'s
  method (build from the unpatched tag in a pinned container, pin the result by SHA-256) and
  [ADR-0017](ADR-0017-curl-se-8-22-0-windows-build-as-a-supplementary-build.md)'s supplementary role;
  [ADR-0080](ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md)'s `debug-build`
  and `libtest` skip reasons.

## Context

The roadmap planning of 2026-11-16 (BL-1288 to BL-1302) found 16 upstream test cases that need an
http/2 or http/3 server half. Six run today (1700, 1701, 1702, 2403, 2500, 2503). Ten do not:

| Case | Why it is skipped | Needs |
| --- | --- | --- |
| 2400, 2401, 2406 | `<features>` lists `Debug` (HTTP/2 over HTTPS) | a `--enable-debug` curl with nghttp2 |
| 2501 | `Debug` (HTTP/3 POST) | a `--enable-debug` curl with ngtcp2 and nghttp3 |
| 358, 359 | `Debug` and `h2c` (alt-svc translated to HTTP/2) | a `--enable-debug` curl with nghttp2 |
| 2402, 2404, 2407, 2502 | `<tool>` is a libtest program (`lib2402`, `lib2404`, `lib2405`, `lib2502`) | a libtest binary linked against libcurl, which for 2404 and 2407 is itself built with the debug hooks |

Upstream curl publishes no Debug binaries, and no packaged build is Debug and unpatched. A Debug
build is therefore made from the `curl-8_21_0` sources. Everything pinned today is pinned by
SHA-256 (ADR-0003), so the build must come out the same bytes every time, as ADR-0076's does.

## Decisions

1. **Build from source, reproducibly, in a pinned container.** `Build-DebugUpstreamCurl.ps1`
   (task BL-1310) copies `Build-OpenLdapUpstreamCurl.ps1`'s method: an Alpine container pinned by
   digest, packages pinned by version, every source pinned by SHA-256 (curl's `.asc` verified
   against Daniel Stenberg's key), `SOURCE_DATE_EPOCH` set to curl 8.21.0's release date and
   `-ffile-prefix-map`, built twice and compared. The tool is `curl` itself, statically linked
   against musl.
2. **Backends.** OpenSSL 3.5.x (the QUIC-capable line ngtcp2 uses for TLS), nghttp2, nghttp3 and
   ngtcp2, all static, at the releases the stunnel/static-curl 8.21.0 build lists (nghttp2 1.69.0,
   nghttp3 1.16.0, ngtcp2 1.23.0) so the Debug build differs from the pinned supplementary static
   build in the debug options and not in the libraries. Curl is configured with `--enable-debug
   --with-openssl --with-nghttp2 --with-ngtcp2 --with-nghttp3 --enable-alt-svc` and none of the
   other optional libraries. These library sources are covered by Stewart's approval as part of
   "the build"; if one cannot be fetched by hash from its project's release page, the task blocks
   with a `Stewart:` reason rather than taking another version.
3. **Platforms: `linux-x64` only.** The container build yields a Linux ELF, which CI's Linux leg
   runs and Windows lanes cannot. Windows keeps `debug-build` on the six cases (50 in all) and
   macOS likewise; a macOS Debug build is not reproducible without a macOS runner, so it is not
   attempted, and this is recorded as a limit, not a refusal: a later ADR may add it when a
   reproducible macOS route exists.
4. **Pinned as a supplementary build.** `UpstreamCurlBuilds.json` gains one `linux-x64` entry with
   `role: supplementary`, `defaultPath: /opt/upstream-curl/8.21.0-debug/curl`, its origin naming
   the script and this ADR, and `features` listing `Debug` (as `curl -V` prints it). It is
   supplementary, so "exactly one reference build per platform" still holds
   (`.github/actions/upstream-curl` selects the reference by `role`) and the Debug build never
   second-guesses a reference build: a case runs with it only when its `<features>` name `Debug`.
5. **How CI obtains it.** As ADR-0076: `.github/actions/upstream-curl` gains a `debug` input
   (Linux only) that restores the build from `actions/cache`, keyed on the script's hash and the
   pin, builds it when absent, and fails unless the SHA-256 equals the pin. Built once per key, not
   per run.
6. **Selecting the build.** The conformance runner chooses the Debug build for a case whose
   `<features>` contain `Debug` when the platform pins one, and otherwise skips `debug-build` as
   now. No other case is moved off its reference build.
7. **Cases run.** 2400, 2401, 2406, 2501, 358 and 359 are run by the Debug build once task BL-1311
   lands. The four libtest cases (2402, 2404, 2407, 2502) **remain unrunnable** by this build:
   they need a libtest program and a runner that runs `<tool>` programs, which is the 350 `libtest`
   skips of ADR-0080 and a separate decision. The build script also builds `tests/libtest` in the
   same tree so a later ADR can pin it, but it is not pinned or run here. Cases skipped for
   `Debug` that need no http/2 or http/3 (the other 44 of the 50) are not part of this task; the
   same build may run them, and BL-1311 reports what it finds without widening itself.
8. **Alternatives rejected.**
   - *Download a Debug build:* none is published.
   - *A distribution package:* patched, and never Debug.
   - *Build on the runner without a container:* the hash would depend on the runner's toolchain,
     so it could not be pinned.
   - *Windows or macOS builds:* no reproducible route; Docker Desktop yields Linux binaries.
   - *Reuse the static-curl build:* it has no Debug hooks.
   - *Using the Curl port or any other curl:* never (ADR-0003).

## Consequences

- Six http/2 and http/3 cases gain a verdict on Linux; Windows and macOS counts do not move.
- A seventh pinned `linux-x64` build exists, supplementary, with a script, a cache key and a hash
  to keep current when Alpine drops a pinned package (ADR-0104 is the precedent).
- Follow-up tasks: BL-1310 builds and pins it, BL-1311 runs the six cases with it, BL-1312
  updates `Upstream-Test-Case-Pass-Rate.md` and its passing lists, and BL-1313 decides how libtests
  are built and run.

## Amendment 1 (2026-11-16, BL-1338): a listed case skipped for want of the binary is not a regression

Decided by Claude under Stewart's delegation.

The release workflow's "Conformance against the published linux-x64" job runs the suite with only
the reference curl, while the one per-platform passing list names the cases that pass where the
Debug build and the libtests binary are installed (CI's "Upstream test cases" job). Nine listed
cases skipped there (358, 359, 2400, 2401, 2406, 2501 as `debug-build`; 2402, 2404, 2502 as
`libtest`) and the guard failed them as regressions.

Decision: the guard (`UpstreamTestCasePassingListComparison.Compare`) takes the skip reasons to
tolerate, and the run passes `debug-build` only when it has no Debug build, and `libtest` and
`libtest-not-built` only when it has no libtests binary. Where the binary is present nothing is
tolerated, so a skip there is still a regression. A tolerated skip is never counted as a pass:
the stated rate and the results file still say `skip`. Rejected: installing both in the published
job (another source build in the release path) and splitting the list by build (two lists to
keep in step).

Separately, BL-1337 set `CURL_DEBUG=all` for every admitted libtest, which makes curl's trace
write `[0-0]` into the `Connection #0 ... left intact` lines that 2402, 2404 and 2502 verify, so
those three failed `file`. Surl was not at fault. The variable is now set for 2407 only, the case
that times out (BL-1336). The three stay on the passing list.
