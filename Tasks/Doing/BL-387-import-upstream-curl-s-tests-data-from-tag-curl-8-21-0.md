---
id: BL-387
title: Import upstream curl's tests/data from tag curl-8_21_0
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-386]
touches: [.gitattributes, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-387 — Import upstream curl's tests/data from tag curl-8_21_0

## Goal

The repository holds upstream curl's `tests/data` from tag `curl-8_21_0` byte for byte, where BL-386's
ADR puts it, with curl's `COPYING` notice beside it. It is copied into the test output, so that
`Surl.Conformance.UnitTests` can read every case.

## Context

- Decision: BL-386's ADR, decision 1 (location, how it is fetched, the notice, line endings). The
  planning assumption is a folder inside `Conformance/Surl.Conformance.UnitTests`. The layout rule (root
  `CLAUDE.md`) forbids a new top-level `tests/`.
- Source: https://github.com/curl/curl at tag `curl-8_21_0` (commit `68720b48`). For example, fetch it
  with `git archive` or a shallow clone into the scratchpad, then copy `tests/data/*` and `COPYING`.
  This is upstream test data, not an upstream curl build, so it needs no pin.
- `.gitattributes` sets `* text=auto`. Test files whose sections say `crlf="yes"` or contain raw CR
  bytes must stay byte-exact, so mark the folder `-text` (or whatever the ADR says).
- `tests/data` also holds non-test files (for example `DISABLED`, `Makefile.am` and `data*.c`). Keep
  whatever the ADR says to keep. `DISABLED` lists the cases upstream itself skips.

## Acceptance criteria

- [ ] The folder holds every `test*` file at the tag. Notes record a count that matches the
      product overview's 2,013, or explain the difference.
- [ ] Every file's bytes equal the tag's. A PowerShell comparison of SHA-256 hashes against the
      fetched tree is recorded in Notes.
- [ ] `COPYING` from the tag sits beside the data, with a short `README.md` naming the tag, the commit
      and the date copied.
- [ ] `.gitattributes` keeps the files byte-exact on checkout on every platform (`git ls-files --eol`
      shows no conversion), and the test csproj copies them to the output directory.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
