---
id: BL-769
title: Exclude the 24 FTP upstream test cases that verify upstream's greeting banner
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-689]
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-05
completed:
---
# BL-769 — Exclude the 24 FTP upstream test cases that verify upstream's greeting banner

## Goal

The 24 FTP-family upstream test cases that fail only because they verify upstream's test-server
greeting banner are listed as `test-server-choice` exclusions, so they no longer count as failures.

## Context

- Found in BL-394's run (Windows, reference build, 2026-11-05): cases 1349-1354, 1357-1362,
  1379-1384 and 1387-1392 fail `file2`. Each writes curl's `-D` headers file (`log/heads<N>` or
  `log/stdout<N>`), which for FTP holds the server's replies, and expects upstream ftpserver.pl's
  ASCII-art greeting (`220-        _   _ ____  _`, ...). Surl's FTP server greets
  `220 surl FTP server ready` (ADR-0052; ADR-0006 section 3, no version or product art).
- ADR-0080 decision 5: a byte the upstream test server chose arbitrarily - "a banner's wording" is
  the ADR's own example - may be listed in `Documentation/Conformance/Upstream-Test-Case-Exclusions.md`,
  one row per case naming the byte, upstream's choice, Surl's answer and why a correct server need not
  send upstream's. BL-689 makes the selection read that file.
- Check first that each case differs only in the greeting lines (strip the greeting from both sides
  by hand and compare); any case that differs elsewhere is a separate defect and becomes a task.

## Acceptance criteria

- [ ] `Upstream-Test-Case-Exclusions.md` has one row for each of the 24 cases, naming the greeting.
- [ ] A local run of `UpstreamTestCasesAgainstSurlTests` on Windows records each as
      `skip test-server-choice`, and the pass-rate file's `win-x64` row counts them in its
      `test-server-choice` column, with the run's counts in Notes.

## Notes

## Log

- 2026-11-05: Created.
