---
id: BL-638
title: Prove with pinned upstream curl that a file uploaded over one protocol is read over another
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-622, BL-723]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] The class exists with one named test per pair the ADR lists, each asserting curl's exit code 0 and the
      fetched bytes equal to the uploaded bytes, in memory and with a data directory.
- [x] A test uploads over one protocol and fetches a file another protocol overwrote, showing the last write wins.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-11-05 (lane 8): Wrote `UpstreamCurlSharesFilesAcrossSurlProtocolsTests` (Integration category, as
  ADR-0100 decision 5 and `.claude/rules/testing.md` say for tests that start a live surl; the Context's
  "no Integration category" is overruled by the ADR, which wins) and a multi-scheme
  `SurlOnLoopback.StartInMemoryAsync(schemes, …)`. One surl serves `ftp`, `http`, `scp`, `sftp` with
  `--allow-uploads --allow-plaintext-auth --throwaway-hostkey --user tester:secret`; curl runs `-sS -f -u`.
  Against pinned curl 8.21.0 on Windows, in memory and with `--directory`: `-T` ftp then GET http passes;
  `-T` sftp then ftp fetch passes; last-write-wins (`-T` ftp, overwrite `-T` sftp, GET http) passes.
- The ADR's third pair, `-T` over `http` (PUT) then fetch over `scp`, cannot pass: surl's HTTP server
  answers every PUT `405 Method Not Allowed` (ADR-0008; ADR-0102 decision table "the HTTP server takes no
  uploads"). BL-723 decides and builds PUT uploads for `surl http`, so this task now depends on it and
  returns to Backlog. The test class is left uncommitted in the working tree for the shift to stash; when
  resumed, rerun it once BL-723 lands (the PUT rows should then pass unchanged).
- 2026-11-05 (lane 3): BL-723 is Done (ADR-0133: `PUT` under `--allow-uploads` answers `201`/`204`). The
  earlier draft was in a shift stash this lane could not reach, so the class was rewritten on the multi-scheme
  `SurlOnLoopback.StartInMemoryAsync` / `StartOverDirectoryAsync` already committed. Four tests, each with an
  in-memory and a `--directory` row: `-T ftp` then GET `http`; `-T sftp` then fetch `ftp`; `PUT http` then
  fetch `scp`; and last-write-wins (`-T ftp`, overwrite with `-T sftp`, GET `http`). Choices: the uploaded
  bytes include CR LF, NUL and 0xFF so a text-mode or truncating transfer fails; FTP and SFTP uploads go to
  `dir/f.bin` with `--ftp-create-dirs` (the ADR's `dir/f` shape), the PUT to the root because ADR-0133 answers
  a missing directory `409`; curl runs `-sS -f` so an HTTP error is a non-zero exit. All 8 rows pass against
  pinned curl 8.21.0 on Windows. Category `Integration` stays, per ADR-0100 decision 5 (the ADR wins over the
  Context line).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Waits on BL-723: surl http answers every PUT 405, so the ADR-0100 PUT-then-scp pair cannot pass yet
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Pinned upstream curl uploads over ftp, sftp and http (PUT) to one surl and reads the same bytes back over http, ftp and scp, in memory and with --directory, last write wins
