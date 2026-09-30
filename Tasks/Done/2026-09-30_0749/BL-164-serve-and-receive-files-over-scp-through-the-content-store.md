---
id: BL-164
title: Serve and receive files over SCP through the content store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-163, BL-155, BL-232]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-041
created: 2026-09-29
completed: 2026-09-30
---
# BL-164 — Serve and receive files over SCP through the content store

## Goal

An `scp` `exec` on an SSH session channel downloads a file from, or uploads one into, the
content store as BL-155's ADR decides, under every exposure option and limit, so
`curl scp://...` and `curl -T file scp://...` have a server to talk to.

## Context

- Decision: BL-155's ADR (command forms, path mapping, modes and times, every refusal and its
  text); ADR-0006 sections 2 and 5 (hidden entries answered as missing, uploads need
  `--allow-uploads`, `--max-filesize` with the partial upload deleted); ADR-0031 decision 5
  (`/.surl` never served or written).
- Content: `Surl.Content.UnitLibrary/ContentStore.cs` (reads, `IContentFileSystem`, the upload
  path through a temporary file and rename). Add the `ProjectReference` to
  `Surl.Content.UnitLibrary` (ADR-0002 allows it). Tests use `InMemoryContentFileSystem`.
- The handler plugs into BL-163's channel seam and is tested at channel level: byte scripts of
  channel data built from the SCP protocol as BL-155's ADR describes it, with a `README.md`
  beside the fixtures saying they are spec-derived and that BL-172 proves them against pinned
  curl (a disagreement there becomes a new task, never a changed expectation, ADR-0003).
- Code to copy (never expectations): the Curl port's `Scp/` (`ScpFileHeaderReader`,
  `ScpCommand`, `ScpRemotePath`), turned to the server's side.

## Acceptance criteria

- [x] Fast tests cover: a download (the `C` line, the bytes, the acknowledgements, exit status
      0); an upload accepted with `--allow-uploads` and refused without it; an upload over
      `--max-filesize` refused with nothing left behind; a missing file, a hidden dot-file, a
      directory and `/.surl/lock` each answered as BL-155's ADR says; a path escaping the served
      root refused.
- [x] No error text sent to the peer holds a local path or an exception message (ADR-0006
      section 3).
- [x] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror`
      is clean; the fast tests pass with no socket opened;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- `SftpPath` became `SshContentPath`, with the store mapping both SCP and SFTP use, so the two
  never map a path differently (ADR-0054 decision 1 covers both).
- The `--max-filesize` check runs after the upload is opened, so a hidden, `/.surl` or
  otherwise `NotPermitted` target answers `Permission denied` before `File too large`, the order
  decision 4's table gives; the opened session is disposed unwritten, leaving nothing behind.
- The SCP byte scripts are inline in `ScpDownloadHandlerTests` and `ScpUploadHandlerTests`, not
  fixture files; `Fixtures/README.md` says they are spec-derived and that BL-172 proves them.
- A path climbing above the root with `..` stops at the root (decision 1), so it lands inside the
  served root; a path the store cannot map (a backslash, a NUL) is `No such file or directory`.
- `ScpControlLine` split into small helpers to keep every member at cyclomatic complexity 10 or
  less; `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`: 100% line, 100% branch,
  0 failing members, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. SCP download and upload served through the content store, 100% coverage
