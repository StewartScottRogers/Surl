---
id: BL-1084
title: Confirm BL-568's httptls+srp cases 320, 321, 322 and 324 on CI's Linux and macOS legs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-568]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-12
completed:
---
# BL-1084 — Confirm BL-568's httptls+srp cases 320, 321, 322 and 324 on CI's Linux and macOS legs

## Goal

A CI run containing BL-568's commits lists upstream test cases 320, 321, 322 and 324 by number in the
Linux and macOS conformance result lines, and the outcomes are recorded here.

## Context

BL-568 built the `httptls+srp` server half (`UpstreamTestCaseTlsSrpServerHalf`) and proved the Windows
skip (`feature-missing:TLS-SRP`); its CI criteria need a pushed commit a lane cannot make, so they moved
here, as BL-371's went to BL-724. Read runs with `gh run list --workflow CI` and `gh run view <id> --log`.

## Acceptance criteria

- [ ] The URL of a `CI` run containing BL-568's commits is in Notes, with the result of cases 320, 321,
      322 and 324 on `ubuntu-latest` and `macos-latest`, each listed by number.
- [ ] Failing cases are grouped by cause into new tasks filed with `task-planner`, their IDs in Notes;
      passing ones are added to the platform's passing list and pass-rate file.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Deferred. No CI run contains BL-568's commit d8640ec7 yet (latest CI runs c5ad69c1 and 6eb8657d are not descendants); look again after the shift merges it to master and CI passes
