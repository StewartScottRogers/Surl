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
completed: 2026-11-04
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

- [x] The folder holds every `test*` file at the tag. Notes record a count that matches the
      product overview's 2,013, or explain the difference.
- [x] Every file's bytes equal the tag's. A PowerShell comparison of SHA-256 hashes against the
      fetched tree is recorded in Notes.
- [x] `COPYING` from the tag sits beside the data, with a short `README.md` naming the tag, the commit
      and the date copied.
- [x] `.gitattributes` keeps the files byte-exact on checkout on every platform (`git ls-files --eol`
      shows no conversion), and the test csproj copies them to the output directory.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Followed ADR-0080 decision 1: `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/` holds
  `data/`, `certs/`, `COPYING`, `README.md`, `SHA256SUMS` and `Import-UpstreamTestCases.ps1`.
  The importer cloned `curl-8_21_0` (sparse, depth 1, `core.autocrlf=false`), `git rev-parse HEAD`
  was `68720b4837284335b2d63cb358f8f6ce65f5bc55`, and it imported 2,049 files: 2,039 data, 9 certs
  (8 `.prm` and `test-ca.cnf`), and `COPYING`.
- Count: 2,013 `test<N>` files at the tag and 2,013 imported, which matches the product overview.
  `data/` also holds `DISABLED` and the 25 data files. Left out, as the ADR says: `.gitignore` and
  `Makefile.am`.
- Hashes: a PowerShell `Get-FileHash` (SHA-256) loop compared each imported file with its source
  in the fetched tree (`tests\data`, `tests\certs`, `COPYING`). Result: compared 2049, mismatches 0.
  No imported data file holds a CR byte at this tag; `-text` still matters for binary bytes and
  later tags.
- `.gitattributes`: added `data/**`, `certs/**`, `COPYING` and `SHA256SUMS` as `-text`. `COPYING` and
  `SHA256SUMS` were added beyond the ADR's two lines because the hash test reads them too. After
  `git add`, `git ls-files --eol` shows all 2,050 files as `i/lf w/lf attr/-text`, so there is no
  conversion.
- The csproj uses `None Update` (the SDK already includes them as `None`) with
  `CopyToOutputDirectory=PreserveNewest`. `UpstreamTestCasesTests` (4 fast tests) checks each
  hash against `SHA256SUMS`, that the output has exactly the listed files, the 2,013 count, and
  `DISABLED` and `COPYING`. The tests read files beside the assembly. They are not marked
  Integration because ADR-0080 asks for a fast test, so a normalisation fails the default run on
  every platform.
- Verified: `dotnet build -warnaserror` gave 0 errors, every fast test project passed (Conformance
  189), and `dotnet format --verify-no-changes` is clean for the project.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Upstream curl's tests/data at curl-8_21_0 (2,013 cases) imported byte-exact with COPYING, SHA256SUMS, importer and a fast hash test
