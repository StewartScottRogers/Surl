---
id: BL-856
title: Exclude the MQTT RETAIN-flag and TFTP missing-file upstream test cases as test-server choices
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-689]
touches: [Documentation/Conformance]
requirement: none
created: 2026-11-05
completed:
---
# BL-856 — Exclude the MQTT RETAIN-flag and TFTP missing-file upstream test cases as test-server choices

## Goal

The six upstream test cases that fail only on a byte upstream's test server chose - MQTT 1190,
1192, 2204, 3018, 1640 and TFTP 283 - are listed as `test-server-choice` exclusions.

## Context

- Found in BL-396's run (Windows, reference build, 2026-11-05).
- **MQTT 1190, 1192, 2204, 3018, 1640** fail `protocol` on one byte: upstream's mqttd sends the
  subscriber's `PUBLISH` with RETAIN clear (`30 0c ...`); Surl's server sets it (`31 0c ...`),
  as MQTT 3.1.1 section 3.3.1.3 requires of a message sent because a new subscription was made
  (ADR-0014). Every other line of their logs matches.
- **TFTP 283** reads `/invalid-file` and expects exit 69 (ERROR 2, access violation); upstream's
  tftpd refuses any name without a case number that way. Surl answers a missing file with
  ERROR 1, `File not found` (RFC 1350; ADR-0013), and curl exits 68.
- ADR-0080 decision 5 lets such a case be listed in
  `Documentation/Conformance/Upstream-Test-Case-Exclusions.md`, one row per case naming the byte,
  upstream's choice, Surl's answer and why a correct server need not send upstream's. BL-689 makes
  the selection read that file (as BL-776 waits on it for the FTP banner cases).

## Acceptance criteria

- [ ] `Upstream-Test-Case-Exclusions.md` has one row for each of 1190, 1192, 2204, 3018, 1640 and
      283, each naming the byte, upstream's choice, Surl's answer and the specification section.
- [ ] The Windows run records them `skip test-server-choice`, and
      `Upstream-Test-Case-Pass-Rate.md`'s `win-x64` row counts them in its test-server-choice column.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-12: Backlog -> Doing.
