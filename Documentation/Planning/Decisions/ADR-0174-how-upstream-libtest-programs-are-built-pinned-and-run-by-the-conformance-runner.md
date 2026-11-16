# ADR-0174 — How upstream's libtest programs are built, pinned and run by the conformance runner

- **Status:** Accepted
- **Date:** 2026-11-16
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-16, in
  BL-1313. It needs no new download: the libtest sources are `tests/libtest` in the `curl-8_21_0`
  tag, the same tag [ADR-0173](ADR-0173-a-debug-upstream-curl-8-21-0-build-from-source-for-the-http-2-and-http-3-test-cases-that-need-one.md)
  already builds and `Import-UpstreamTestCases.ps1` already clones, both covered by Stewart's
  2026-11-16 approval ("download"). No package, no threshold change.
- **Applies:** ADR-0173 decisions 1, 4 and 7 (the container build, the supplementary pin, the four
  cases left unrunnable) and [ADR-0080](ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md)'s
  `libtest` skip reason.

## Context

ADR-0080 skips every case whose `<tool>` is not `curl` as `libtest`. In the imported data, 362 test
files name a `<tool>`: 243 `lib%TESTNUMBER`, 112 `lib<N>`, 6 `tool%TESTNUMBER`, 1 `unit<N>`
(ADR-0080 counts 350 skipped once its earlier reasons apply). A libtest program is a C program in
`tests/libtest` linked against libcurl; runtests runs it as `<tool> <command arguments>` in place of
the `curl` tool, then checks the same `<verify>` sections. Surl's server half is already the thing
under test, so the work is a build, a pin and a runner path, not new server code.

By their `<server>` the cases are about 225 `http`, 45 `ftp`, 10 `http`+`http-proxy`, 9 `rtsp`,
5 `imap`, 5 `https`, 3 `smtp`, 3 `http/2`, 2 `sftp`, 2 `mqtt`, 1 `http/3`, and 39 with no server.
About 50 also list `Debug` (the library's debug hooks).

## Decisions

1. **Build the libtest programs in the ADR-0173 container, in the same tree.** The Debug build
   script (`Build-DebugUpstreamCurl.ps1`, BL-1310) gains a step that runs `make -C tests/libtest`
   against the static Debug libcurl it just built, so the programs link the same library, with the
   same `--enable-debug` hooks, as the pinned `curl`. It is reproducible by the same rules (pinned
   digest and packages, `SOURCE_DATE_EPOCH`, built twice and compared). No second, release libcurl
   is built: a case that does not need Debug runs fine against a Debug library, and one library
   keeps one set of bytes to pin.
2. **Pin by SHA-256, as a new kind `libtest`.** `UpstreamCurlBuilds.json` gains one `linux-x64`
   entry with `kind: libtest`, `role: supplementary`, a `defaultPath` of
   `/opt/upstream-curl/8.21.0-debug/libtest` (a directory) and a `programs` map of file name to
   SHA-256. Each program is pinned by its own hash, not by a source tree hash: a source hash would
   not detect a toolchain change that alters the binary. The runner refuses any program whose hash
   differs, as `Record-CurlExchange.ps1` refuses any other curl. The libtest sources are not copied
   into the repository; the tag and the pinned build script identify them.
3. **How the runner runs one.** For a case whose `<tool>` names a libtest program the runner
   resolves the name (`lib%TESTNUMBER` becomes `lib<N>`), looks it up in the entry's `programs`,
   verifies the hash, and runs it where it would run `curl`: the case's `<command>` arguments after
   the usual `%HOSTIP` and port substitutions, the same working directory and environment, and the
   same `<verify>` comparison of protocol bytes, stdout and exit code. With no pinned `libtest`
   entry on the platform, or no such program, the skip reason stays `libtest`, so counts stay
   comparable.
4. **Linux only, like ADR-0173.** Windows and macOS keep `libtest` as a skip reason and the
   pass-rate file says why. CI's Linux leg obtains the programs through the cached build of
   ADR-0173 decision 5, with one more hash check.
5. **Run first.** Stage 1: 2402, 2404, 2407 (which runs `lib2405`) and 2502, the cases ADR-0173
   named; they prove a libtest runs end to end over http/2 and http/3, where Surl already answers.
   Stage 2: the `http` and `https` libtest cases (about 230). Stage 3: the `ftp`, `http-proxy`,
   `rtsp`, `imap`, `smtp`, `mqtt` and `sftp` ones, each when its server row is built. Each stage is
   a batch of case numbers the runner admits and the pass-rate file reports.
6. **Stay unrunnable, and why.**
   - The one `unit<N>` program: it keeps `unittest`, it tests internals with no server.
   - The 39 cases with no `<server>`: `no-server-half`, nothing for Surl to answer.
   - Any case with a `<precheck>`, `<postcheck>`, Perl command or `%PERL`: ADR-0080's more specific
     reasons stay, since a libtest case is only runnable once those pass.
   - A program that does not link in the container: listed by name in the pass-rate file as
     `libtest-not-built:<name>`, never silently dropped.
   - Windows and macOS, by decision 4.
   - A case that fails on a Surl gap is a failure with its reason, never an exclusion (ADR-0080
     decision 5's list is the only place for those).
7. **Alternatives rejected.**
   - *Pin by git tree hash of `tests/libtest`:* detects a source change only.
   - *Build the programs on each runner:* the binary would depend on the runner, so it could not be
     pinned (ADR-0173 decision 8).
   - *A release libcurl for the programs:* a second library to build and pin, and about 50 cases
     need Debug.
   - *Rewrite the libtest programs in C# on the Curl port:* a verdict by the port, never (ADR-0003).
   - *Keep every libtest case skipped:* it is the largest block of unplayed upstream tests and the
     server halves they need exist.

## Consequences

- Four named cases, then hundreds, gain a verdict on Linux; Windows and macOS counts do not move.
- The ADR-0173 build script grows one step and the Debug pin gains a sibling entry with a hash per
  program, which change only when Alpine drops a pinned package.
- One new skip reason, `libtest-not-built:<name>`, appears; the others are unchanged.
- Follow-up tasks are listed in BL-1313's Notes.
