---
id: BL-902
title: Prove with pinned upstream curl the exit codes of chosen-error faults on SFTP, SCP and SMB
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-795, BL-803]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-902 — Prove with pinned upstream curl the exit codes of chosen-error faults on SFTP, SCP and SMB

## Goal

Integration tests run the pinned upstream curl 8.21.0 against a live `surl` given `--fault error` rules for
`sftp`, `scp` and `smb`, and pin the exit code curl gives for each.

## Context

- BL-795 (ADR-0135) built the answers and tested them at channel and connection level; a live `surl` applies
  them only once BL-803 composes the fault plan into the running server.
- ADR-0135, "What upstream curl does with these answers", lists what is already measured for the same bytes
  (SFTP `NO_SUCH_FILE` 78, `PERMISSION_DENIED` 9, `FAILURE` 79; an SCP error line 78 on a download and 25 on an
  upload; an SMB `CREATE` refusal 78) and what has never been seen: an SCP upload refused before its first
  `\0`, and an SMB status sent as an NTSTATUS with `SMB_FLAGS2_NT_STATUS` set.
- Use the existing `UpstreamCurlTransfersFilesWithSurlOverSftpTests`, `...OverScpTests` and `...OverSmbTests`
  harnesses, `[TestCategory("Integration")]`, the pinned build only (Inconclusive without it), never the Curl
  port (ADR-0003).

## Acceptance criteria

- [ ] Named integration tests: an SFTP download under `error;scheme=sftp;path=/a.txt;reply=3 No`, an SFTP
      upload under a `2` rule, an SCP download and an SCP upload under an `scp` rule, an SMB download and upload
      under `reply=C0000022`, each asserting the exit code as measured with the curl version stated.
- [ ] ADR-0135's "What upstream curl does with these answers" states the measured codes.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-05: Created.
