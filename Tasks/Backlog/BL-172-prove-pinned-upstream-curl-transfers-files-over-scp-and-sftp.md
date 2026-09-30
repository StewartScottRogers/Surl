---
id: BL-172
title: Prove pinned upstream curl transfers files over scp and sftp against surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-171, BL-167, BL-168, BL-169, BL-170]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-042
created: 2026-09-29
completed:
---
# BL-172 — Prove pinned upstream curl transfers files over scp and sftp against surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned
upstream curl 8.21.0 builds log in to a live `surl` over `scp` and `sftp` and download, upload,
list and run quote commands exactly as BL-154's and BL-155's ADRs expect, and record which SSH
algorithms each platform's build negotiated.

## Context

- The cases: BL-155's ADR's list of command lines and expected results, plus BL-154's: a
  password login (`-u user:pass`), a public-key login (`--key`/`--pubkey` with a key pair the
  test generates in a temporary directory), a refused login (curl's exit code measured, e.g.
  67), the host-key check satisfied as the ADR decides (`--hostpubsha256` of surl's key, or
  `-k`), and `--compressed-ssh`.
- Harness: `Surl.Conformance.UnitTests/SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`,
  `AccountsFile.cs`; `Assert.Inconclusive` when the pinned build is absent (BL-020's rule); the
  Linux and macOS legs run on CI (ADR-0016). Isolate curl from the operator's own
  `known_hosts` (point `HOME`/`USERPROFILE` at a temporary directory for the run, or whatever
  BL-154's ADR says).
- Algorithms: curl's preference decides the kex and cipher, surl's configured host key decides
  the host-key algorithm. Read the negotiated algorithms from surl's verbose notes (BL-154's ADR)
  and write in Notes which algorithms each platform's build negotiated; include one run per
  host-key type (ECDSA, RSA, Ed25519) by configuring only that key. An algorithm no run
  negotiates is covered by unit tests only, and Notes say so. Record here the Linux and macOS
  builds' KEXINIT lists, as BL-154's ADR says.
- Any disagreement with the pinned build is fixed in `Surl.Protocol.Ssh` through a new task
  filed by `task-planner`, never by changing the expected result (ADR-0003); list them in the
  Log.

## Acceptance criteria

- [ ] Integration tests exist for every case in Context and pass on Windows with the pinned
      build present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] Notes list the algorithms negotiated per platform and per host-key type, and the Linux and
      macOS KEXINIT lists once CI has run.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a
      socket.

## Notes

## Log

- 2026-09-29: Created.
