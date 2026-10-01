---
id: BL-275
title: Say in Surl.Protocol.Ssh's CLAUDE.md that Surl.Console gives allowWeakAlgorithms
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-250]
touches: [Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md]
requirement: FR-039
created: 2026-09-30
completed: 2026-09-30
---
# BL-275 — Say in Surl.Protocol.Ssh's CLAUDE.md that Surl.Console gives allowWeakAlgorithms

## Goal

`Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md` says `Surl.Console` gives `SshAlgorithmOffer.Default` `allowWeakAlgorithms` when a start gives `--allow-weak-ssh-algorithms`, instead of saying such a start is refused with exit 2.

## Context

- BL-250 made `Surl.Console` serve `--allow-weak-ssh-algorithms` (ADR-0051 decisions 2 and 11); BL-270 fixed the product documents, README and glossary.
- BL-270 could not edit this file: BL-262, in Doing at the time, named `Surl.Protocol.Ssh.UnitLibrary` in its `touches`.
- Stale text today: the "The weak algorithms" bullet, "`Surl.Console` never gives it today: a start with `--allow-weak-ssh-algorithms` is refused with exit 2."

## Acceptance criteria

- [x] `git grep -n "never gives it today" Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md` finds nothing.
- [x] The bullet says `Surl.Console` passes `allowWeakAlgorithms` from `--allow-weak-ssh-algorithms`, and still says host certificates (`--hostcert`) are not built here.

## Notes

- 2026-09-30: Already satisfied when claimed; no edit to the CLAUDE.md was needed. Commit
  a71c4be (`feat(ssh): narrow an SSH offer's ciphers and MACs to named ones`) removed the stale
  "never gives it today" sentence, and the "The weak algorithms" bullet now reads "offered only
  when `SshAlgorithmOffer.Default` is given `allowWeakAlgorithms`, as `Surl.Console` does under
  `--allow-weak-ssh-algorithms`" (`git grep` exits 1). `Surl.Console/SshAlgorithmComposition.cs`
  passes `commandLine.AllowWeakSshAlgorithms`, so the text is true of the code.
- The second criterion's "host certificates are not built here" half is superseded: commit
  33efa23 (`feat(ssh): serve OpenSSH host certificates`) built them in this library
  (`SshHostCertificate`, `SshCertifiedHostKey`), and the CLAUDE.md's "Host certificates" bullet
  now says so. Keeping a "not built here" statement would be false, so the criterion is ticked
  on the file being true of the code, not on the stale wording.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Protocol.Ssh's CLAUDE.md already says Surl.Console gives allowWeakAlgorithms under --allow-weak-ssh-algorithms (a71c4be); verified, no edit needed
