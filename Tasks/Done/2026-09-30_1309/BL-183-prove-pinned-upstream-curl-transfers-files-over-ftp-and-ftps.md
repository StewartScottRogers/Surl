---
id: BL-183
title: Prove pinned upstream curl transfers files over ftp and ftps against surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-182]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-037
created: 2026-09-29
completed: 2026-09-30
---
# BL-183 — Prove pinned upstream curl transfers files over ftp and ftps against surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned
upstream curl 8.21.0 builds complete every command line BL-173's ADR lists against a live
`surl` over `ftp` and `ftps`, with the exit codes and output the ADR expects.

## Context

- The cases: BL-173's ADR's list, covering at least a passive download (EPSV and, with
  `--disable-epsv`, PASV), an active download (`-P -`, and `--disable-eprt`), `-I`, `-r`,
  `-C -`, a directory listing with `--list-directories`, `-l`, `-T` with `--allow-uploads`,
  `--ftp-create-dirs`, a `-Q` delete and rename, a login with an account over `ftps://` and over
  `ftp://` with `--ssl-reqd` (`--self-signed`, curl `-k`), a plain-text login refused (curl's
  exit code, measured) and accepted with `--allow-plaintext-auth`, and curl's anonymous login
  with and without `--allow-anonymous`.
- Harness: `Surl.Conformance.UnitTests/SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`,
  `AccountsFile.cs`, `TestCertificateAuthority.cs`; `Assert.Inconclusive` when the pinned build is
  absent; Linux and macOS legs on CI (ADR-0016). Uploads land in a temporary `--directory` the
  test reads back.
- Any disagreement with the pinned build is fixed in `Surl.Protocol.Ftp` (or the library at
  fault) through a new task filed by `task-planner`, never by changing the expected result
  (ADR-0003); list them in the Log.

## Acceptance criteria

- [x] Integration tests exist for every case in Context and pass on Windows with the pinned
      build present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a
      socket.

## Notes

- The case list is ADR-0052 decision 12 (BL-173's ADR). Three integration classes in
  `Surl.Conformance.UnitTests`, one per concern, 43 cases in all:
  - `UpstreamCurlLogsInToSurlOverFtpTests`: anonymous login with and without
    `--allow-anonymous` (0 / 67), an account over plaintext refused (67) and accepted with
    `--allow-plaintext-auth` (0), a wrong password (67), `--ssl-reqd`, `--ftp-ssl-control` and
    `--ssl-reqd --ftp-ssl-ccc` over `ftp://` with `--self-signed` (0, CCC refused), implicit
    `ftps://` (0), and `--ssl-reqd` against a listener with no certificate (64).
  - `UpstreamCurlFetchesFromSurlOverFtpTests`: EPSV, `--disable-epsv` (PASV), `-P -` (EPRT),
    `-P - --disable-eprt` (PORT); every `--ftp-method`; `-I`; `-r 0-4`, `-r 3-6`; `-C 5`; `-B`;
    an absent file (78); `LIST`/`NLST` with listings off (19); `LIST`, `-l` and `-X MLSD` with
    `--list-directories`; `--max-connections 1` while another client holds it (28).
  - `UpstreamCurlUploadsToSurlOverFtpTests`: `-T` without `--allow-uploads` (25) and with it,
    passive and active; `-a -T`; `-C - -T` onto a partial file; `--ftp-create-dirs`;
    `--max-filesize 4` (70, nothing written); `-Q` DELE, RNFR/RNTO, `MKD` then `-RMD` around a
    download; `-Q DELE` without `--allow-uploads` (21); `-Q SITE CHMOD` with and without it (21).
- Every case passed against the pinned Windows reference build on the first run: no
  disagreement with the pinned build, so no fix task was filed.
- Choice: `-B`'s output is compared with its line end trimmed, because ADR-0052 row 39 measured
  that the Windows tool writes `-B` output in text mode (CRLF) while surl sends the bytes
  unconverted; the OpenSSL builds on Linux and macOS write LF. One test then holds on every
  platform without pinning a platform-specific byte.
- Choice: the `-Q DELE` and `-Q RNFR/RNTO` cases add `--list-directories`, as decision 12's
  table says, so the listing curl asks for after the quotes succeeds and the exit is 0.
- The Linux and macOS legs run on CI only (lanes test on Windows); the tests pin no
  Windows-only path, text or certificate.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Pinned upstream curl completes all 43 ADR-0052 decision 12 ftp and ftps cases against surl with the expected exit codes and output
