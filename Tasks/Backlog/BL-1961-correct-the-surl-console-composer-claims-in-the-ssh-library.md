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
completed:
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

- [ ] Each of the four sentences names the project that does what it says, checked against source.
- [ ] Only comments and `CLAUDE.md` text change, `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-27: Created.
