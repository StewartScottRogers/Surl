---
id: BL-638
title: Prove with pinned upstream curl that a file uploaded over one protocol is read over another
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-622]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-638 — Prove with pinned upstream curl that a file uploaded over one protocol is read over another

## Goal

A conformance test class shows, with pinned upstream curl 8.21.0 at both ends against one `surl` serving several
schemes, that a file uploaded over one protocol is read byte for byte over another.

## Context

- Decision: BL-622's ADR, decision 5 (the pairs and the class name; planning
  `UpstreamCurlSharesFilesAcrossSurlProtocolsTests` in `Conformance/Surl.Conformance.UnitTests`).
- Pairs (the ADR's list wins): `curl -T f ftp://…/dir/f` then `curl http://…/dir/f`; `curl -T f sftp://…` then
  `curl ftp://…`; `curl -T f http://…` (PUT) then `curl sftp://…`; each in memory and with `--directory`.
  One `surl` process with several listen URLs, `--allow-uploads`, accounts from `--user`.
- How existing classes start a `surl` and run a pinned build: `SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`,
  `PinnedUpstreamCurlOverSsh.cs`, `UpstreamCurlUploadsToSurlOverFtpTests.cs`, `UpstreamCurlTransfersFilesWithSurlOverSftpTests.cs`.
  Upstream curl is located only through `UpstreamCurlBuilds.json`; a missing build is Inconclusive
  (`UpstreamCurlUnavailability`). No `Integration` category: these run in CI where a pinned build exists.
- Tests pass on Windows, Linux and macOS; where a reference build lacks a protocol (macOS has no `rtsp`, check
  `UpstreamCurlBuildFeatures` for `sftp`), the row is Inconclusive with the reason.

## Acceptance criteria

- [ ] The class exists with one named test per pair the ADR lists, each asserting curl's exit code 0 and the
      fetched bytes equal to the uploaded bytes, in memory and with a data directory.
- [ ] A test uploads over one protocol and fetches a file another protocol overwrote, showing the last write wins.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-04: Created.
