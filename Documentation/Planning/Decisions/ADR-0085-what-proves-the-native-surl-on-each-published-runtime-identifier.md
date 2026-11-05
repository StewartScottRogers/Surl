# ADR-0085 — What proves the native surl on each published runtime identifier

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-358. BL-359 builds decision 2's switch in `SurlOnLoopback`, BL-360 wires decisions 1, 2, 3
  and 5 into the workflows, and BL-361 adds decision 4's runs if Stewart approves BL-357.
- **Amends:** nothing. [ADR-0016](ADR-0016-the-linux-and-macos-upstream-curl-builds-and-how-ci-obtains-them.md)
  (how CI obtains and verifies a pinned build), [ADR-0017](ADR-0017-curl-se-8-22-0-windows-build-as-a-supplementary-build.md)
  (reference and supplementary roles) and [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md)
  (upstream curl is the only oracle) stand, and this ADR uses them.

## Context

Success criterion 4 of `Documentation/Product/Product-Overview.md` says a published `surl` runs
with no .NET runtime installed; its "Constraints" say publishing is native ahead-of-time, one file,
for Windows, Linux and macOS on x64 and Arm64; Phasing row 6 says native publish on every platform.
NFR-006 in `Documentation/Product/Requirements.md` states the same. On 2026-11-04 none of it is
proven by a check:

- `.github/workflows/release.yml` publishes `win-x64` (`windows-latest`), `win-arm64`
  (`windows-11-arm`), `linux-x64` (`ubuntu-22.04`), `linux-arm64` (`ubuntu-22.04-arm`), and both
  `osx-x64` and `osx-arm64` on `macos-latest`, which is Arm64. Its smoke test runs only
  `surl --version`, accepts exit 0 or 2 under a stale comment ("until Phase 1 lands it exits 2"), and
  is skipped for `osx-x64`, because an x64 binary on that runner would only show that Rosetta works,
  if it is installed at all.
- `surl --version` now exits 0 and its first line is `surl <version> (<runtime identifier>)`, for
  example `surl 1.0.0 (win-x64)`.
- `Surl.Conformance.UnitTests` runs surl in-process: `SurlOnLoopback` calls `Program.RunAsync`, the
  entry point the executable uses, and stops it by cancelling a token. So every conformance test
  proves the JIT-compiled code, never the native binary. `Surl.Console` stops on SIGINT or SIGTERM
  and then exits 0 (`Program.CreateStopOnSignal`).
- `ci.yml` runs the conformance tests against the pinned reference build on `ubuntu-latest`
  (`linux-x64`) and `macos-latest` (`osx-arm64`). Its Windows leg has no pinned build: the `win-x64`
  reference build is Git for Windows' `curl.exe`, whose SHA-256 the runner image's Git does not
  match, so Windows conformance runs where the pinned file is, on Stewart's machine and in lanes.
- Upstream curl is pinned for `win-x64`, `linux-x64` and `osx-arm64` only. BL-357 asks Stewart
  whether to download stunnel/static-curl's 8.21.0 builds for `linux-arm64`, `win-arm64` and
  `osx-x64`.

## Decision

### 1. The smoke test every runtime identifier runs

Each of the six legs of `release.yml` runs the same smoke test against the binary it just
published, on a runner of the binary's own operating system and architecture (decision 3). It is
one PowerShell script, `Test-PublishedSurl.ps1` at the repository root, that BL-360 writes; every
runner image used has PowerShell 7, and a leg whose image lacks it installs it first. The script
fails the leg on the first check that fails, naming the check:

1. **It is one native file.** The publish directory holds `surl` (`surl.exe` on Windows) and,
   at most, its native symbol file (`surl.pdb`, `surl.dbg`, or `surl.dSYM`). Any `*.dll` beside it,
   `surl.runtimeconfig.json` or `surl.deps.json` fails the check: those are what a framework-dependent
   or self-contained, non-native publish leaves, and a .NET host cannot start an app without its
   `runtimeconfig.json`.
2. **It starts and says what it is.** `surl --version` exits 0, and its first line is exactly
   `surl <VERSION> (<rid>)`, where `<VERSION>` is the version the leg passed to `dotnet publish`
   and `<rid>` is the leg's runtime identifier. Exit 2 is no longer accepted, and the stale comment
   goes. The `(<rid>)` check is what shows an `osx-x64` binary ran as x64 rather than an Arm64
   binary under the wrong name.
3. **It serves.** The script writes a file of 65,536 bytes it generates (bytes 0 to 255 repeated)
   into a new temporary directory, starts `surl --directory <dir> http://127.0.0.1:0/`, reads its
   standard output until the `Listening on <url>` status line (failing after 30 seconds), and sends
   one HTTP/1.1 `GET` for the file over a `System.Net.Sockets.TcpClient`. The answer must be status
   `200` with a body equal to the file, byte for byte. The request bytes are the ones the pinned
   reference build sends for `curl http://127.0.0.1:<port>/<file>`, copied from an existing
   `Record-CurlExchange.ps1` recording of that request, so the smoke test asks what upstream curl
   asks. It is not a conformance claim: it proves the binary runs and its sockets, file reading and
   HTTP/1.1 writing work natively. Conformance is decision 2.
4. **It stops as documented.** On Linux and macOS the script sends SIGTERM (`kill -TERM`) and
   surl must exit 0 within 10 seconds. On Windows, where a process outside surl's console cannot send
   it Ctrl+C, the script ends it with `Stop-Process` and checks only that it is gone; Ctrl+C on
   Windows stays proven by `ProgramTests.CreateStopOnSignal_Signal_CancelsTheDefaultHandlingAndTheStopToken` and by hand.

How it shows that no .NET runtime is used:

- **Linux** (`linux-x64`, `linux-arm64`): checks 2 to 4 run a second time inside a Docker container
  of `ubuntu:22.04`, the image the binary's glibc floor names (`release.yml`'s
  header: Ubuntu 22.04, glibc 2.35), with the publish directory mounted read-only and nothing
  installed in it. That image has no .NET, so a binary that needed one could not start. Docker is
  on both `ubuntu-22.04` runner images. The script also fails if `ldd` lists `libhostfxr`,
  `libhostpolicy` or `libcoreclr`.
- **Windows and macOS** (`win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`): GitHub's hosted images
  cannot be emptied of .NET and offer no container of their own operating system, so the script runs
  checks 2 to 4 with every `DOTNET_*` environment variable removed, `DOTNET_ROOT` included, and every
  `PATH` entry holding a `dotnet` executable removed. With check 1's missing `runtimeconfig.json`,
  that leaves the binary no runtime to find. On macOS the script also fails if `otool -L` lists
  `libhostfxr` or `libcoreclr`; on Windows, if `surl.exe`'s import table (read with
  `System.Reflection.PortableExecutable.PEReader`) names `hostfxr.dll` or `coreclr.dll`.

### 2. Surl.Conformance runs against the published binary

Yes. The same suite proves the native binary, unchanged, through one switch:

- **The switch** is the environment variable `SURL_EXECUTABLE`, holding the full path of a
  published `surl`. Unset or empty, `SurlOnLoopback` runs surl in-process exactly as it does today.
  Set, every `SurlOnLoopback` start launches that file as a process with the same arguments it would
  have passed to `Program.RunAsync`, reads `Listening on ` status lines from its standard output, and
  collects its standard error as `Log`. An environment variable, not a test run parameter, because
  `dotnet test` passes it through unchanged to every test host on every platform, and because a lane
  can set it in a shell without editing a `.runsettings` file.
- **A missing file** fails each test at its start with a message naming the path. A process that
  exits before its status line fails the test with its exit code and standard error, as the
  in-process run does with `surl exited <code> before it listened.`
- **Stopping**: on Linux and macOS disposal sends SIGTERM (by running `kill -TERM <pid>`, so the test
  project needs no platform invoke) and waits up to 10 seconds; on Windows, and on Linux and macOS
  when that wait ends, it kills the process tree. Disposal then waits for the process to exit, so no
  `surl` outlives its test. The tests never assert surl's exit code, so killing on Windows loses
  nothing they check.
- **Where it runs**:
  - `linux-x64` and `osx-arm64`: in `release.yml`'s dry run, a job after each leg's publish, on the
    same runner label, obtains and verifies the pinned reference build (and, on Linux, the OpenLDAP
    build) by the steps `ci.yml` already runs (ADR-0016), then runs
    `dotnet test Conformance/Surl.Conformance.UnitTests -c Release --filter "FullyQualifiedName~Surl.Conformance"`
    with `SURL_EXECUTABLE` naming the leg's published binary. BL-360 moves those obtain-and-verify
    steps into one composite action under `.github/actions/` that both workflows use, so the two
    copies cannot drift.
  - `win-x64`: CI has no pinned Windows build (Context), so this run is local: before a release tag
    is asked for, the conformance suite runs on Stewart's machine with `SURL_EXECUTABLE` naming a
    `win-x64` publish, and its result is recorded in the release task. `release.yml`'s `win-x64` leg
    runs decision 1 only.
  - `linux-arm64`, `win-arm64`, `osx-x64`: decision 4.

The test host itself runs on the .NET SDK `setup-dotnet` installs; that is the test harness, not
surl. Decision 1, not this run, is what shows surl needs no runtime.

### 3. The runner for osx-x64

`osx-x64` is built and tested on `macos-15-intel`, GitHub's x64 macOS image, not on `macos-latest`.
Native AOT builds for the host's own architecture without a cross toolchain, and decision 1 then
runs on real x64 hardware, so its `(<rid>)` check means something. `macos-latest` keeps `osx-arm64`.
When GitHub announces the retirement of its last x64 macOS image, a new ADR decides what replaces it
(a cross-architecture build on Arm64 with the smoke test under Rosetta 2 is the planned fallback,
since Rosetta proves less than real x64 hardware but more than nothing).

### 4. linux-arm64, win-arm64 and osx-x64, with and without BL-357

- **If BL-357 is declined**, these three run decision 1 on their own runners (`ubuntu-22.04-arm`,
  `windows-11-arm`, `macos-15-intel`), and, in the same release dry run, the fast tests
  (`dotnet test -c Release --filter "TestCategory!=Integration"`) on the same runner. The fast tests
  are JIT-compiled, not the native binary, but they are the only check of the hand-built
  cryptography and protocol code on Arm64 and on x64 macOS, where hardware intrinsics and endianness
  assumptions differ from the three `ci.yml` runners. No upstream curl runs there, so no claim is made
  that upstream curl completes an exchange with those three binaries; `Requirements.md` NFR-006 says
  so.
- **If BL-357 is approved**, BL-361 pins the three stunnel/static-curl 8.21.0 builds with
  `"role": "reference"`, and each of the three legs also runs decision 2's conformance job against its
  published binary with its own pinned build, through the same composite action. The fast tests stay.

### 5. What runs on every pull request, and what only in the release dry run

- **`ci.yml`, on every pull request and push**: unchanged. The fast tests on `windows-latest`,
  `ubuntu-latest` and `macos-latest`, and the in-process conformance suite on Linux and macOS. A
  native publish takes minutes per runtime identifier and every production project is already held
  AOT-compatible at build time by `IsAotCompatible` and `VerifyAotCompatibility`
  (`Directory.Build.props`), so the trim and AOT analyzers already fail a pull request that would
  break the native build. `ci.yml` gains no publish.
- **`release.yml`'s dry run** runs decisions 1, 2 (`linux-x64`, `osx-arm64`) and 4 on all six
  runtime identifiers. It runs:
  - on a pull request that touches `release.yml`, `Application/Surl.Console/**`,
    `Directory.Build.props`, `global.json` (as today), and also `Conformance/**`,
    `Test-PublishedSurl.ps1`, `UpstreamCurlBuilds.json` or `.github/actions/**`, which decide what
    the dry run checks;
  - weekly, on a `schedule` trigger against `master`, so a change to a library that the paths above
    do not name, and that breaks the native binary at run time, is found within a week;
  - by hand (`workflow_dispatch`).
- **Before a release tag** is asked of Stewart, a `release.yml` dry run of the commit to be tagged is
  green on all six runtime identifiers, and decision 2's local `win-x64` run passed. A tag push runs
  the same checks again before the release job, since that job `needs` them.

## Alternatives considered

- **Keep `--version` as the whole smoke test.** Rejected: it shows the binary starts, not that its
  sockets, TLS-free HTTP path and file reading work natively, and it would never catch a trimmed-away
  code path that only a request reaches.
- **Prove "no runtime" by uninstalling .NET on the hosted runner.** Rejected: the images install it
  in several places and the test host needs it afterwards for decision 2. A clean container on Linux
  and a scrubbed environment plus a missing `runtimeconfig.json` elsewhere prove the same thing
  without fighting the image.
- **Run conformance against the published binary in `ci.yml` on every pull request.** Rejected for
  cost: two native publishes and a second conformance run per pull request, for a failure the AOT
  analyzers mostly catch at build time. The weekly schedule bounds what slips through.
- **A `.runsettings` parameter instead of `SURL_EXECUTABLE`.** Rejected: an environment variable
  reaches the test host the same way on every platform and in every shell, needs no file, and is
  what a lane or Stewart can set by hand.
- **Use the stunnel `win-x64` build, a supplementary build, as CI's Windows reference.** Rejected:
  ADR-0017 pins it for the protocols and features it names only, never to second-guess the reference
  build, and the reference build is Git for Windows' Schannel curl.
- **Build `osx-x64` on `macos-latest` and smoke-test it under Rosetta 2.** Kept only as the fallback
  of decision 3: while a real x64 image exists, it proves more.

## Consequences

- BL-359 adds the `SURL_EXECUTABLE` switch to `SurlOnLoopback` (decision 2).
- BL-360 writes `Test-PublishedSurl.ps1`, the composite action, the conformance jobs, the
  `macos-15-intel` leg, the fast-test runs on the three extra runners, the widened paths and the
  weekly schedule (decisions 1 to 5). It keeps the package names, so `DOWNLOAD.md`, `install.sh` and
  `install.ps1` stay as they are.
- BL-361, if BL-357 is approved, adds the three pins and their conformance jobs (decision 4).
- NFR-006 in `Documentation/Product/Requirements.md` names these checks as what proves it.
- Until BL-357 is approved, the claim that upstream curl completes an exchange with the published
  binary covers `linux-x64` and `osx-arm64` in CI and `win-x64` locally, not the other three.
