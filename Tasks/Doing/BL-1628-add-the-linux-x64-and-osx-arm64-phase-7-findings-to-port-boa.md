---
id: BL-1628
title: Add the linux-x64 and osx-arm64 Phase 7 findings to Port-Board-Handoff.md
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1625, BL-1626]
touches: [Documentation/Conformance/CurlPort/Port-Board-Handoff.md]
requirement: FR-057
created: 2026-11-24
completed:
---
# BL-1628 — Add the linux-x64 and osx-arm64 Phase 7 findings to Port-Board-Handoff.md

## Goal

`Documentation/Conformance/CurlPort/Port-Board-Handoff.md` holds every linux-x64 and osx-arm64 Phase 7 finding as an entry in the port's own task shape, so Stewart can file them on the port's board with the 24 win-x64 entries.

## Context

ADR-0089 Amendment 8: "any new finding goes into `Port-Board-Handoff.md` in decision 7's form". Today the handoff has 24 numbered entries (win-x64), and its "What the new commit fixed" section says the linux-x64 findings (Ldap.md L-1 to L-4, Mail.md M-1, Ftp.md FTP-1, Ssh.md SSH-2, Upstream-Test-Cases.md UTC-5) are "not yet as entries here", and that osx-arm64 "compared nothing". BL-1626 settles the final linux-x64 list (some may be removed as run-to-run noise, some added) and BL-1625 the osx-arm64 list; take the lists from their family files' `### Findings (linux-x64)` and `### Findings (osx-arm64)` tables as those tasks leave them, not from this paragraph.

Each entry follows the existing entries' shape (`## <n>. <imperative title>`, then `### Goal`, `### Context` naming the Surl commit, Surl test and family file, `### Acceptance criteria`). A finding that shows on several platforms with one root is one entry naming each platform; one that matches an existing win-x64 entry's root extends that entry's context instead of duplicating it. Filing the entries on the port's board is Stewart's (ADR-0089 decision 7); no Surl session writes into the port's repository, and the port is never an oracle (ADR-0003).

## Acceptance criteria

- [ ] Every row under a `### Findings (linux-x64)` or `### Findings (osx-arm64)` heading in `Documentation/Conformance/CurlPort/*.md` is covered by a numbered entry in `Port-Board-Handoff.md`, each entry naming the finding IDs it covers.
- [ ] The file's opening count of findings and its "What the new commit fixed" section state the new total and no longer say the linux-x64 findings are "not yet as entries here" or that osx-arm64 "compared nothing".
- [ ] Each new entry names its platform(s), the Curl port commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, and the `curl-port-phase-7` run it was seen in.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
