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
completed:
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

- [ ] Integration tests exist for every case in Context and pass on Windows with the pinned
      build present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a
      socket.

## Notes

## Log

- 2026-09-29: Created.
