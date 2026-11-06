---
id: BL-1005
title: Record the ADR for the remaining TLS 1.3 groups curl offers over QUIC
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-055
created: 2026-11-06
completed:
---
# BL-1005 — Record the ADR for the remaining TLS 1.3 groups curl offers over QUIC

## Goal

An accepted ADR records what each pinned upstream curl build sends with `--curves <group>` for the seven
groups ADR-0082 decision 3 does not serve, decides that all seven are served and in which order, and adds
`Surl.Cryptography.Curve448.UnitLibrary` and `Surl.Cryptography.Sm2.UnitLibrary` to ADR-0002 decision 3's
table, so the code tasks split from BL-545 can cite it.

## Context

- Split from BL-545, which timed out after 120 minutes on 2026-11-06 with this ADR already written but
  uncommitted. Its lane's work is the stash whose message is `darkfactory BL-545 20261106-033059` in the
  main checkout (commit `e132aaaa3985ff32f9f5f6e8838cbc123b5082ef`; find its index with `git stash list`).
  Below, `S` is that commit.
- Reuse, untracked: `git show "S^3:Documentation/Planning/Decisions/ADR-0142-the-remaining-tls-1-3-groups-curl-offers-over-quic.md"`.
  Its measurements (static 8.21.0 OpenSSL 4.0.1 on Windows and Linux, curl.se 8.22.0_2 LibreSSL 4.3.2 on
  Windows, measured 2026-11-06 with `Record-CurlExchange.ps1 -QuicInitial`) and its decisions stand.
  **ADR-0142 is now taken** (`ADR-0142-how-surl-names-a-files-media-type-from-its-extension.md`), and the
  shared branch has reached at least ADR-0146: save it under the next free number at the time you run,
  rewrite every `ADR-0142` inside it, and replace "BL-545 creates the projects" with the split tasks:
  BL-1006 (ML-KEM-1024), BL-1007 (Curve448), BL-1029 (Sm2), BL-1008 to BL-1030 (`Surl.Tls13`).
- Reuse, tracked: `git diff "S^" "S" -- Documentation/Planning/Decisions/ADR-0002-mirror-the-curl-ports-project-map.md Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md`
  adds ADR-0002's amendment line and ADR-0082's "Amended by" line and corrects its `supported_groups` row
  (`11EE` is curveSM2MLKEM768; SecP384r1MLKEM1024 is `11ED`). Re-make the lines by hand with the new ADR
  number rather than `git apply`, since both files have since gained lines.
- Add the new ADR to `Documentation/Planning/Decisions/README.md`'s index if the index lists ADRs.
- Stand-in: not applicable; this records which TLS groups the HTTP/3 server accepts and adds no store,
  event or fault.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/`, numbered next free, "Decided by Claude under
      Stewart's delegation", holds the measurement table for all seven groups and both build families,
      the server's order (SecP256r1MLKEM768, SecP384r1MLKEM1024, curveSM2MLKEM768, x448, curveSM2,
      ffdhe2048, ffdhe3072) and decision 3's library table.
- [ ] ADR-0002 carries an "Amended" line naming the new ADR, the two new `Cryptography/` libraries
      (each referencing nothing) and `Surl.Tls13.UnitLibrary`'s right to reference both.
- [ ] ADR-0082 carries an "Amended by" line naming the new ADR, and its `supported_groups` row names
      `11EE` curveSM2MLKEM768.
- [ ] No file outside `Documentation/Planning/Decisions/` changes.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses the ADR written in stash `darkfactory BL-545 20261106-033059`.
