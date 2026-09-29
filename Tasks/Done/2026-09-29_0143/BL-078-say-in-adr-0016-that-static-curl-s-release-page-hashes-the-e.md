---
id: BL-078
title: Say in ADR-0016 that static-curl's release page hashes the executables, not the archives
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-028]
touches: [Documentation/Planning/Decisions/ADR-0016-the-linux-and-macos-upstream-curl-builds-and-how-ci-obtains-them.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-078 — Say in ADR-0016 that static-curl's release page hashes the executables, not the archives

## Goal

ADR-0016 states truthfully which file each quoted SHA-256 belongs to and how BL-028
verified the download.

## Context

- ADR-0016 calls `153ca463…` and `04e0e69b…` the release page's SHA-256 values and, in
  decision 2, asks BL-028 to confirm "the archive's SHA-256" equals "the value on the
  release page". BL-028 found (Notes) that the release page's "Checksums of binaries"
  table hashes the extracted `curl` executables; the archives' SHA-256 values are
  GitHub's asset digests (`e955f211…` Linux musl, `fdfe9ca5…` macOS arm64).
- A document that says something false of the code or the procedure is a defect (root
  `CLAUDE.md`, "Say what it does"). An accepted ADR is amended with a dated note, not
  rewritten.

## Acceptance criteria

- [x] ADR-0016's Context says the release page's quoted values hash the `curl`
      executables, and gives both archives' GitHub asset digests.
- [x] Decision 2 carries a dated amendment: the executable's hash is checked against the
      release page, the archive's against GitHub's asset digest.

## Notes

- Full archive digests read 2026-09-29 from `gh api repos/stunnel/static-curl/releases/tags/8.21.0` (read-only); prefixes, suffixes and sizes equal BL-028's Notes. The Context paragraph is corrected in place with an inline dated note, and decision 2 keeps its text with a dated amendment below it, so the original wording stays readable. Docs-only: no `.cs` or project file changed, so the `verify` skill was not needed; `dotnet build` and fast tests were run anyway.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0016 says the release page hashes the curl executables, gives both archives' GitHub asset digests, and amends decision 2 to check each against its own source
