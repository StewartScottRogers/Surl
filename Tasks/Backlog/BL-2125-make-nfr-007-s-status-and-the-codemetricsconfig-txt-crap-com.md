---
id: BL-2125
title: Make NFR-007's status and the CodeMetricsConfig.txt CRAP comment claim only checks that exist
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md, CodeMetricsConfig.txt]
requirement: none
created: 2026-11-28
completed:
---
# BL-2125 — Make NFR-007's status and the CodeMetricsConfig.txt CRAP comment claim only checks that exist

## Goal

NFR-007's status in `Documentation/Product/Requirements.md` names only a check that exists, and the CRAP comment in `CodeMetricsConfig.txt` says the `coverage-auditor` agent reports the limit's gaps rather than enforcing it.

## Context

Split from BL-2120 (2026-11-28), whose run timed out. NFR-007 says it is enforced by `Directory.Packages.props` and the `ci.yml` build, but nothing fails on a second package or reference: central package management only fails a reference with no version entry. The `CodeMetricsConfig.txt` comment says the `coverage-auditor` agent "enforces" the CRAP limit of thirty; the agent reports gaps and files tasks. BL-2120's run wrote both edits, saved in the stash named `darkfactory BL-2120 20261128-060304`; find its index N with `git stash list` (match the message, not the number). Reuse them:

- `CodeMetricsConfig.txt`: `git diff "stash@{N}^" "stash@{N}" -- CodeMetricsConfig.txt | git apply`. Check afterwards with `git diff` that only comment lines changed.
- NFR-007's row and the `Last updated:` line of `Documentation/Product/Requirements.md`: read them with `git show "stash@{N}:Documentation/Product/Requirements.md"` and copy those two lines in with the Edit tool. Do not `git apply` the whole file: its other rows belong to the sibling task (NFR-008 to NFR-020).

Re-check the copied NFR-007 text against `Directory.Packages.props`, `Directory.Build.props` and the projects before keeping it. The follow-up task that adds the check NFR-007 waits on ("Fail a fast test when the solution gains a second NuGet package or package reference") is already filed and depends on this one. Adding a check is not part of this task, and no threshold changes. Lanes may not run git checkout or git stash. Stand-in: nothing to add; documentation only.

## Acceptance criteria

- [ ] NFR-007's status names a check that fails when a second package is added, or is `Draft` with what it waits on in brackets.
- [ ] The `CodeMetricsConfig.txt` comment says the `coverage-auditor` agent reports the CRAP limit's gaps, and every threshold line in the file is unchanged.

## Notes

## Log

- 2026-11-28: Created.
