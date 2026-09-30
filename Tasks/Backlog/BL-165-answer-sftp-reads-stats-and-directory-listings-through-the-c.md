---
id: BL-165
title: Answer SFTP reads, stats and directory listings through the content store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-163, BL-155, BL-230]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-042
created: 2026-09-29
completed:
---
# BL-165 — Answer SFTP reads, stats and directory listings through the content store

## Goal

The `sftp` subsystem answers SFTP version 3's read side through the content store -
`INIT`, `REALPATH`, `STAT`, `LSTAT`, `FSTAT`, `OPEN` for reading, `READ`, `CLOSE`, `OPENDIR` and
`READDIR` - as BL-155's ADR decides, so `curl sftp://host/file` and `curl sftp://host/dir/`
have a server to talk to.

## Context

- Decision: BL-155's ADR (version, path mapping, attributes, long-name format, status codes and
  messages, handle limits, the SFTP packet bound). Specification: draft-ietf-secsh-filexfer-02.
- Exposure (ADR-0006 section 2, ADR-0015): a listing without `--list-directories` and a hidden
  entry are answered exactly as missing; `/.surl` never (ADR-0031 decision 5); symbolic links
  per `--follow-symlinks`. What a listing holds: ADR-0009.
- Content: `Surl.Content.UnitLibrary/ContentStore.cs`; add the `ProjectReference` if BL-164 did
  not. Tests use `InMemoryContentFileSystem` and a fake `TimeProvider`.
- Plugs into BL-163's channel seam; tested at channel level with spec-derived byte scripts and
  a fixtures `README.md` saying BL-172 proves them against pinned curl (ADR-0003).
- Code to copy (never expectations): the Curl port's `Sftp/` (`SftpAttributes`,
  `SftpPacketType`, `SftpStatusCode`, `SftpDirectoryEntry`), turned to the server's side.

## Acceptance criteria

- [ ] Fast tests cover each request in the Goal with its success answer, and: a missing file, a
      hidden file, `/.surl`, a directory listing with and without `--list-directories`, a read
      past the end (`SSH_FX_EOF`), an unknown handle, the handle limit, a packet over the bound,
      and a request type not in the read side (answered as the ADR says until BL-166).
- [ ] No status message sent to the peer holds a local path or an exception message (ADR-0006
      section 3).
- [ ] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean; the fast tests pass
      with no socket opened; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
