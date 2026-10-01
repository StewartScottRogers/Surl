---
id: BL-213
title: Document FTP, SCP and SFTP in the product overview, glossary, README and roadmap
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-172, BL-183]
touches: [Documentation/Product/Product-Overview.md, Documentation/Wiki, Documentation/Planning/Roadmap.md, README.md, Documentation/Product/Requirements.md, Surl.Protocol.Ftp.UnitLibrary/CLAUDE.md, Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-213 — Document FTP, SCP and SFTP in the product overview, glossary, README and roadmap

## Goal

Every document names FTP, FTPS, SCP and SFTP as they are once Phase 2 is proven - the product
overview, the glossary, the README, the roadmap, the requirements and the two protocol
libraries' `CLAUDE.md` files - so nothing an agent reads says Phase 2 is still to come.

## Context

- The work: BL-148 to BL-183 and the ADRs they wrote (BL-148, BL-154, BL-155, BL-173).
- `Documentation/Product/Product-Overview.md`: "Also in scope", Authentication ("Built today"
  gains the FTP and SSH logins); "Layers" and "Project layout" (the four hand-built libraries
  exist); "Phasing" is unchanged. `Documentation/Planning/Roadmap.md`: a Milestone 2 entry with
  its status, what it delivered and its ADRs, in the shape of Milestone 0's.
  `Documentation/Wiki/Glossary.md`: one term per concept, e.g. data connection, passive and
  active mode, host key, authorized key, SCP, SFTP subsystem (root `CLAUDE.md`: one concept,
  one name). `README.md`: the protocol list and an example command line for each.
  `Documentation/Product/Requirements.md`: FR-036 to FR-042 name what satisfies them.
  `Surl.Protocol.Ftp.UnitLibrary/CLAUDE.md` and `Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md`: what
  the library holds now, its references, its ADRs.
- The `align-and-document` agent's rule: every statement true of the code as it is now; intent
  written as intent. Read the code, do not trust the tasks.

## Acceptance criteria

- [x] Each file in `touches` states what is built for FTP, FTPS, SCP and SFTP, naming the ADRs,
      and none says Phase 2 is unbuilt.
- [x] `Documentation/Wiki/Glossary.md` defines each new term once, and the code and documents use
      those names.
- [x] `Documentation/Planning/Roadmap.md` has a Milestone 2 entry with status, delivery and
      decisions.

## Notes

- Delivered by `align-and-document` (2026-09-30). The documents say Phase 2 is built and proven
  with the pinned Windows reference build (ADR-0052 decision 12 for FTP/FTPS, ADR-0054
  decision 16 and ADR-0051 for SCP/SFTP); the Linux and macOS builds' SSH negotiation is
  stated as predicted. Weak SSH algorithms (BL-250, BL-261) and host certificates (BL-222)
  are stated as not served: `surl` exits 2 on those options today.
- Glossary terms take the names the code already uses (data connection, passive/active mode,
  explicit/implicit FTPS, host key, authorized key, SFTP subsystem, ...), so no rename was
  needed inside this task's `touches`.
- Requirements' Status column stays `Draft`: no row has moved past it and no other value is
  defined. Stale lines found while editing (Milestone 1, mail and Kerberos library lines,
  FR-035's topic and category counts) were corrected in place.
- Misalignments in code and ADR-0002, outside `touches`, filed as BL-264. The glossary's
  `--auth` default and README's `--directory` line are left for BL-214 (mail documents).

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Product overview, glossary, README, roadmap (Milestone 2), requirements FR-036..FR-042 and the FTP and SSH CLAUDE.md files now describe FTP, FTPS, SCP and SFTP as built, with their ADRs
