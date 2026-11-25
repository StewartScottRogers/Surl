---
id: BL-1648
title: Add ADR-0089 Amendment 9's 'not measurable on <platform>' lines to the Phase 7 family files
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1629]
touches: [Documentation/Conformance/CurlPort]
requirement: FR-057
created: 2026-11-24
completed: 2026-11-24
---
# BL-1648 — Add ADR-0089 Amendment 9's 'not measurable on <platform>' lines to the Phase 7 family files

## Goal

Every Phase 7 family file says, in its linux-x64 and osx-arm64 sections, which of its cases are not measurable there and why, as ADR-0089 Amendment 9's closing condition 4 requires.

## Context

Amendment 9 (BL-1627) closes Phase 7 on a platform only when each not-measurable row appears in the platform's section as "not measurable on <platform>: <reason>". BL-1629 found no family file with that wording: the osx-arm64 sections of `Ldap.md` and `Rtsp.md` say Inconclusive with the locator's message, and `Libcurl.md` and `Extended.md` say "no result for this family appears in the artifact". The rows are: `OpenLdap_` (osx-arm64), RTSP (osx-arm64), `WinLdap_` (both), Libcurl rows (both), `[OSCondition(OperatingSystems.Windows)]` cases (both) and the extended clients (out of Phase 7, both).

## Acceptance criteria

- [x] `Ldap.md` (osx-arm64: `OpenLdap_` and `WinLdap_`; linux-x64: `WinLdap_`), `Rtsp.md` (osx-arm64), `Libcurl.md` (both) and `Extended.md` (both) each carry "not measurable on <platform>: <reason>" with Amendment 9's reason.
- [x] `Http.md`, `Ftp.md` and `Ssh.md` carry the same line for their `[OSCondition(OperatingSystems.Windows)]` cases on both platforms.
- [x] `Roadmap.md`, `Product-Overview.md`, `Requirements.md` FR-057 and `CurlPort/README.md` say Phase 7's closing condition 4 holds, once it does.

## Notes

Filed by BL-1629, which states in those four documents that condition 4 is open and names this task.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Every Phase 7 family file carries Amendment 9's 'not measurable on <platform>' lines; closing condition 4 documented as holding
