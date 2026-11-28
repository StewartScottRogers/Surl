---
id: BL-1961
title: Correct the Surl.Console composer claims in the SSH library comments and notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1961 — Correct the Surl.Console composer claims in the SSH library comments and notes

## Goal

The SSH library's `CLAUDE.md` and XML comments name the project that really composes the server and reads its host key
files.

## Context

Found while working BL-1949 (2026-11-27). Text only; no behaviour change. Line numbers are as of that date. These name
`Surl.Console` and have not been checked against the code: `CLAUDE.md:33` and `:48`, `SshHostKeyFile.cs:13`,
`SshHostCertificate.cs:79`. Check each against `Surl.Composition` and `Surl.Console`; a sentence that is true stays.
BL-1950 covers only this library's broken ADR links.

## Acceptance criteria

- [x] Each of the four sentences names the project that does what it says, checked against source.
- [x] Only comments and `CLAUDE.md` text change, `dotnet build` is clean and the fast tests pass.

## Notes

- All four sentences were false; each now names `Surl.Composition`. `SshHostKeyComposition` reads the `--hostkey` and
  `--hostcert` files through `seams.ReadStartFile ?? File.ReadAllBytes` (`SurlServerComposition.Start.cs`), so the
  file sentences also say "from disk unless the host gives another reader". `SshAlgorithmComposition` passes
  `AllowWeakSshAlgorithms` to `SshAlgorithmOffer.Default`.
- Dropped "(BL-171)" from the `SshHostKeyFile` summary: the composer it created has since moved out of `Surl.Console`.
- No other mention of `Surl.Console` remains in the library's `.cs`, `.md` or `.csproj` files.
- Verify: build 0 warnings, format check passed, fast tests 30377 passed, 0 failed, 3 skipped. Integration tests not
  run (comment and prose text only).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The SSH library's comments and CLAUDE.md name Surl.Composition as the composer and reader of the host key and certificate files
