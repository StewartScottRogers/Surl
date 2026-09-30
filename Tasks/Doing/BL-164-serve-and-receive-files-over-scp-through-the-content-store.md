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
completed:
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

- [ ] Fast tests cover: a download (the `C` line, the bytes, the acknowledgements, exit status
      0); an upload accepted with `--allow-uploads` and refused without it; an upload over
      `--max-filesize` refused with nothing left behind; a missing file, a hidden dot-file, a
      directory and `/.surl/lock` each answered as BL-155's ADR says; a path escaping the served
      root refused.
- [ ] No error text sent to the peer holds a local path or an exception message (ADR-0006
      section 3).
- [ ] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror`
      is clean; the fast tests pass with no socket opened;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
