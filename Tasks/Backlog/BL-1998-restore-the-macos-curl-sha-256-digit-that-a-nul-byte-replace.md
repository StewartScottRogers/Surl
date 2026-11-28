---
id: BL-1998
title: Restore the macOS curl SHA-256 digit that a NUL byte replaced in ADR-0053, ADR-0055 and ADR-0056
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md, Documentation/Planning/Decisions/ADR-0055-how-the-imap-server-answers-upstream-curl.md, Documentation/Planning/Decisions/ADR-0056-how-the-pop3-server-answers-upstream-curl.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1998 — Restore the macOS curl SHA-256 digit that a NUL byte replaced in ADR-0053, ADR-0055 and ADR-0056

## Goal

ADR-0053, ADR-0055 and ADR-0056 give the pinned macOS upstream curl SHA-256 in full and hold no NUL byte, so
ripgrep and git treat them as text.

## Context

Found while working BL-1977 (2026-11-27) and checked that day. In `Documentation/Planning/Decisions/`, each of these
files holds exactly one `0x00` byte where the first character of the `curl-macos-arm64-8.21.0.tar.xz` SHA-256 should
be; the text after the byte starts `4E0E69BCD3BD`:

- `ADR-0053-how-the-smtp-server-answers-upstream-curl.md`, line 420.
- `ADR-0055-how-the-imap-server-answers-upstream-curl.md`, line 656.
- `ADR-0056-how-the-pop3-server-answers-upstream-curl.md`, line 382.

Take the missing character from a pinned source, never from memory or a guess: the macOS entry's `sha256` in
`UpstreamCurlBuilds.json`, and the `osx-arm64` line of `Documentation/Conformance/Conformance-Off-Windows.md`. Both
read `04E0E69BCD3BD814EC093551A0447AC14EDEEA9D395B468A2025DCB3F766EEBF` on 2026-11-27. This repairs a corrupted
byte and changes no recorded decision, so no amendment note is needed. If the Edit tool cannot match the NUL byte,
replace that one byte with PowerShell and change nothing else, line endings included.

Documentation only; no behaviour, store, event or fault changes.

## Acceptance criteria

- [ ] For each of the three files, `([IO.File]::ReadAllBytes((Resolve-Path <file>))) -contains 0` in PowerShell returns `False`.
- [ ] In each of the three files the macOS SHA-256 equals the macOS entry's `sha256` in `UpstreamCurlBuilds.json`.
- [ ] `git diff --text --numstat` against the starting commit shows one line added and one removed in each of the three files.

## Notes

## Log

- 2026-11-27: Created.
