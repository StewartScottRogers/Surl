# ADR-0016 — The Linux and macOS upstream curl builds, and how CI obtains and verifies them

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28.
  Stewart approved downloading upstream curl 8.21.0 builds for Linux and macOS on
  2026-09-28 (product overview, open question 2, recorded by BL-023); which builds, and
  how CI obtains them, he left to this ADR.

## Context

ADR-0003 pins upstream curl by file SHA-256 and names builds for Linux and macOS as
needed but not yet pinned. `UpstreamCurlBuilds.json` pins one build today: Git for
Windows' upstream curl 8.21.0 (`win-x64`, Schannel). The `CI` workflow runs the fast
tests on `windows-latest`, `ubuntu-latest` and `macos-latest`; the conformance tests in
`Surl.Conformance.UnitTests` are in the `Integration` category, which CI filters out, and
they report Inconclusive where no pinned build is present (BL-020). Lanes run on Windows
only, so the Linux and macOS pins cannot come from a lane's own curl.

The runners CI uses today are `linux-x64` (`ubuntu-latest`) and `osx-arm64`
(`macos-latest`), in the portable runtime identifiers `UpstreamCurlLocator.CurrentPlatform`
produces.

What was checked on 2026-09-28:

- **The curl project ships no Linux or macOS binary.** https://curl.se/download.html:
  "The curl project mostly provides source packages." Its current release is 8.22.0; the
  8.21.0 source archive remains at https://curl.se/download/curl-8.21.0.tar.xz.
- **stunnel/static-curl publishes static 8.21.0 builds.** Release `8.21.0` of
  https://github.com/stunnel/static-curl ("Static cURL 8.21.0 with HTTP3", built by its
  GitHub Actions workflow on 2026-06-25) has `curl-linux-x86_64-musl-8.21.0.tar.xz`,
  `curl-linux-x86_64-glibc-8.21.0.tar.xz` and `curl-macos-arm64-8.21.0.tar.xz`. The
  release page's "Checksums of binaries" table gives the SHA-256 of each extracted `curl`
  executable, not of the archive (musl
  `153ca463957609117d21a848be29b70691b85f9e5cc9370c7daa037b839a4e45`, macOS arm64
  `04e0e69bcd3bd814ec093551a0447ac14edeea9d395b468a2025dcb3f766eebf`). The archives'
  SHA-256 values are GitHub's asset digests for the release (musl
  `e955f211202ded2536164588331acfc987dc4b7857efa3577717b1ffeab22029`, 4,023,640 bytes;
  macOS arm64 `fdfe9ca5bc60d615b0a379864056f61437a3e9ed50ab746a05abad7c50be766f`,
  3,197,904 bytes). *(Amended 2026-09-29 by BL-078: this paragraph first called the two
  executable hashes the archives' SHA-256 values on the release page; BL-028 found they
  hash the executables.)* Its build scripts
  (`curl-static-cross.sh`, and `curl-static-mac.sh` for macOS) download
  `https://github.com/curl/curl/archive/refs/tags/<tag>.tar.gz` - the upstream tag, here
  `curl-8_21_0` - and apply no patch to curl's sources; the only file they add is a
  `.checksrc` for curl's own style checker. curl is configured `--enable-static
  --disable-shared --with-openssl` with nghttp2, nghttp3, ngtcp2, libssh2, brotli, zstd,
  zlib, libidn2, libpsl and c-ares, SMB enabled and LDAP disabled, and linked statically
  against OpenSSL 4.0.1. The release notes give the resulting lines:
  - protocols: `dict file ftp ftps gopher gophers http https imap imaps ipfs ipns mqtt
    mqtts pop3 pop3s rtsp scp sftp smb smbs smtp smtps telnet tftp ws wss`
  - features: `alt-svc asyn-rr AsynchDNS brotli ECH HSTS HTTP2 HTTP3 HTTPS-proxy HTTPSRR
    IDN IPv6 Largefile libz NTLM PSL SSL SSLS-EXPORT threadsafe TLS-SRP UnixSockets zstd`

  *What the builds print (added 2026-09-29 by BL-085):* CI run
  https://github.com/StewartScottRogers/Surl/actions/runs/36537650411 (BL-079) ran
  `curl --version` on each pinned executable. The `linux-x64` build prints the lines
  above. The `osx-arm64` build prints **no `rtsp`** among its protocols (`... pop3 pop3s
  scp sftp smb smbs smtp smtps telnet tftp ws wss`) and adds the **`AppleSecTrust`**
  feature. `UpstreamCurlBuilds.json` carries each build's printed values, and ADR-0026
  decides how RTSP conformance runs on macOS as a result.
- **Distribution packages patch curl** (Debian, Ubuntu, Homebrew's formula applies
  patches from time to time) and are not at 8.21.0 on the runner images anyway, so they
  are not upstream in the sense ADR-0003 means.

The task (BL-027) asked this ADR to weigh building `curl-8_21_0` from source in CI with
pinned options against a third party's prebuilt 8.21.0.

## Decision

1. **Pin stunnel/static-curl's 8.21.0 static builds**, one per runner platform:

   | `platform` | Asset | Download URL |
   | --- | --- | --- |
   | `linux-x64` | `curl-linux-x86_64-musl-8.21.0.tar.xz` | `https://github.com/stunnel/static-curl/releases/download/8.21.0/curl-linux-x86_64-musl-8.21.0.tar.xz` |
   | `osx-arm64` | `curl-macos-arm64-8.21.0.tar.xz` | `https://github.com/stunnel/static-curl/releases/download/8.21.0/curl-macos-arm64-8.21.0.tar.xz` |

   - **Source:** tag `curl-8_21_0` of https://github.com/curl/curl, unpatched, built by
     the stunnel/static-curl project's GitHub Actions workflow. This is the provenance the
     `origin` field of each pin states.
   - **TLS backend:** OpenSSL 4.0.1, statically linked, on both platforms. The Windows
     reference build uses Schannel; where a TLS answer differs by backend, each platform's
     answer is pinned in its own test, as root `CLAUDE.md` requires for any
     platform-dependent answer.
   - **The musl build for Linux, not the glibc one:** it is fully static, so it runs on
     any `linux-x64` runner image unchanged and depends on no system library whose
     version would move under the pin. Name resolution is c-ares in both variants, so the
     musl build loses nothing Surl measures.
   - **`protocols` and `features` are known from the build itself, not the release page:**
     the pin's `version`, `protocols` and `features` fields are copied from the
     `curl --version` output of the extracted executable, run on its own platform in the
     CI run that pins it (BL-028). The release page's lines, quoted above, are what is
     expected; if the executable says otherwise, the executable wins and the difference
     is recorded in BL-028's Notes.

2. **The SHA-256 pinned is the extracted `curl` executable's**, as for Windows, taken
   with `Get-FileHash -Algorithm SHA256`. A static prebuilt file hashes the same wherever
   it is hashed, so the value does not depend on the runner. BL-028 records, for each
   pin, the archive's SHA-256 as it downloaded it, confirms it equals the value on the
   release page, and records the CI run whose `curl --version` output filled the
   `version`, `protocols` and `features` fields. If the archive's hash differs from the
   release page, BL-028 does not pin it and goes to Blocked for Stewart: a different file
   is a different download to approve.

   *Amendment, 2026-09-29 (BL-078):* the release page hashes the extracted executables,
   not the archives (Context), so the check above is two checks. The extracted `curl`
   executable's SHA-256 - the value pinned - is checked against the release page's
   "Checksums of binaries" table, and the archive's SHA-256 against GitHub's asset
   `digest` for the release (`gh api repos/stunnel/static-curl/releases/tags/8.21.0`).
   BL-028 did both on 2026-09-28 and both matched, for Linux musl and macOS arm64; a
   mismatch in either still means BL-028 does not pin and goes to Blocked for Stewart.

3. **Where the build lives:** `defaultPath` is `/opt/upstream-curl/8.21.0/curl` on both
   platforms - outside the repository, never committed, and never on `PATH`, so it cannot
   shadow the runner's own curl or be run by accident as a bare `curl`. A developer on
   Linux or macOS extracts the same archive to the same path.

4. **How CI obtains it:** by download, never by build. In the existing `test` job of
   `.github/workflows/ci.yml`, on the Linux and macOS legs only
   (`if: runner.os != 'Windows'`):
   1. `actions/cache` restores `/opt/upstream-curl/8.21.0` under the key
      `upstream-curl-<platform>-<pinned sha256>`, so a changed pin never reuses an old
      file.
   2. On a cache miss, a step downloads the asset from the URL above, extracts `curl`,
      and moves it to the `defaultPath` (with `sudo` to create `/opt/upstream-curl`).
   3. **Whether restored or downloaded, a `pwsh` step then verifies the file before
      anything runs it:** it reads `UpstreamCurlBuilds.json`, selects the entry for the
      runner's platform, hashes `defaultPath` with `Get-FileHash -Algorithm SHA256`, and
      exits 1 with the expected and actual values if they differ or no entry exists. Only
      after it passes does any step run the file - the `curl --version` it prints into the
      log first of all. `UpstreamCurlLocator` then verifies the hash again inside the
      tests (ADR-0003, decision 3).

5. **Which job runs the conformance tests:** the same `test` job, in a step after the
   fast tests, on Linux and macOS:
   `dotnet test -c Release --no-build --filter "FullyQualifiedName~Surl.Conformance"`,
   which includes the `Integration` category. It is in the job whose green run on all
   three platforms gates the dark factory's merge to `master`, so a conformance failure
   on Linux or macOS blocks the merge. The Windows leg keeps running only the fast
   tests: the `windows-latest` image ships whatever Git for Windows is current, whose
   curl will not be the pinned 8.21.0 file for long, so Windows conformance runs on
   Stewart's machine and the lanes, where the pinned file is.

6. **A hash that stops matching fails the job loudly and is never re-pinned by CI.** The
   verification step exits 1 and names both hashes; nothing in the workflow writes
   `UpstreamCurlBuilds.json`. Because the files are static and prebuilt, a runner-image
   update does not change them; a mismatch means the asset itself changed (replaced,
   corrupted or deleted upstream) or the cache holds a bad file. Either way it is
   investigated, and moving to any other file is a new pin recorded in a new ADR, with a
   new download approved by Stewart if the file is not the one approved here.

## Consequences

Good:

- One build family for Linux and macOS, from the unpatched upstream tag, whose hash
  covers the whole program: TLS, HTTP/2 and HTTP/3 libraries included, because they are
  linked statically. (On macOS every executable still loads the system's `libSystem`;
  nothing else is loaded at run time.)
- The pins do not move when GitHub updates a runner image, so the merge gate is not
  broken by routine image churn.
- These builds carry `smb`, `smbs`, HTTP/2 and HTTP/3, which the Windows reference build
  lacks. They are still 8.21.0 reference builds; whether the SMB, HTTP/2 and HTTP/3
  servers are also validated on Linux and macOS by them is for the tasks that build those
  servers to decide, beside the supplementary Windows build BL-026 pins.

Costs and caveats:

- The builds come from a third party, not the curl project. Accepted: the curl project
  ships no Linux or macOS binary, the build scripts were read and apply no patch, and the
  pin is by hash, so a later change of the asset cannot enter unnoticed.
- A static OpenSSL build and the Windows Schannel build can answer TLS differently. Such
  answers are pinned per platform.
- The asset could be deleted upstream. The cache keeps a verified copy for CI while it
  is used; if both are gone, the job fails loudly (decision 6) and a new ADR decides.

## Alternatives considered

- **Build `curl-8_21_0` from source in CI with pinned options.** Rejected. Its
  provenance is the cleanest, but the executable's hash depends on the runner's compiler
  and libraries, so every runner-image update would change it and fail the merge gate
  until someone re-pinned - exactly the silent churn the pin exists to stop. A shared
  build would also link the system's OpenSSL at run time, which the executable's hash
  does not cover. A fully static build from source is possible but would re-create
  static-curl's build in this repository, with its dependency pins, for no gain in
  provenance a hash does not already give.
- **The glibc static-curl build for Linux.** Rejected for the musl one: glibc static
  binaries still load NSS modules from the system at run time.
- **Distribution packages or Homebrew.** Rejected: they patch curl and are not 8.21.0.
- **The curl project's Windows build under Wine, or WSL.** Not applicable: neither is a
  Linux or macOS build, and ADR-0003 refuses `wsl.exe`.
- **Running Windows conformance in CI too.** Not now: the runner's Git for Windows is
  not the pinned file, and Windows conformance already runs where the pinned file is.
