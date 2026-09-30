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
completed:
---
# BL-275 — Say in Surl.Protocol.Ssh's CLAUDE.md that Surl.Console gives allowWeakAlgorithms

## Goal

`Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md` says `Surl.Console` gives `SshAlgorithmOffer.Default` `allowWeakAlgorithms` when a start gives `--allow-weak-ssh-algorithms`, instead of saying such a start is refused with exit 2.

## Context

- BL-250 made `Surl.Console` serve `--allow-weak-ssh-algorithms` (ADR-0051 decisions 2 and 11); BL-270 fixed the product documents, README and glossary.
- BL-270 could not edit this file: BL-262, in Doing at the time, named `Surl.Protocol.Ssh.UnitLibrary` in its `touches`.
- Stale text today: the "The weak algorithms" bullet, "`Surl.Console` never gives it today: a start with `--allow-weak-ssh-algorithms` is refused with exit 2."

## Acceptance criteria

- [ ] `git grep -n "never gives it today" Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md` finds nothing.
- [ ] The bullet says `Surl.Console` passes `allowWeakAlgorithms` from `--allow-weak-ssh-algorithms`, and still says host certificates (`--hostcert`) are not built here.

## Notes

## Log

- 2026-09-30: Created.
