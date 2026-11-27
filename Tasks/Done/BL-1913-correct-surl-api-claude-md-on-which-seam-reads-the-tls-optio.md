---
id: BL-1913
title: Correct Surl.Api CLAUDE.md on which seam reads the TLS option files
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Api/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1913 — Correct Surl.Api CLAUDE.md on which seam reads the TLS option files

## Goal

`Application/Surl.Api/CLAUDE.md` states that the `--cert`, `--key` and `--cacert` files are read through `SurlServerHostSeams.ReadStartFile`, as the code does.

## Context

Found while working BL-1912 and checked against the code on 2026-11-27.

- The last bullet under "What it is" says these three files "are still read from disk by `ServerTlsComposition`".
- `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs` passes `seams.ReadStartFile` to `ServerTlsComposition.Compose`, and `Application/Surl.Api.UnitTests/SurlServerTests.Configuration.cs` has a BL-705 test comment saying they are read through the seam.
- The doc comment on `SurlServerCompositionSeams.ReadStartFile` lists every file the composition reads through it.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] `Application/Surl.Api/CLAUDE.md` no longer says any TLS option file is read from disk past the `ReadStartFile` seam.
- [x] The diff changes `Application/Surl.Api/CLAUDE.md` only.

## Notes

- Checked against the code on 2026-11-27: `ServerTlsComposition.Compose` hands its `readFile` to
  `ClientTrustAnchorFileLoader.Load` and `ServerCertificateFileLoader.Load`, which read the disk only when it is null.
- Edited directly rather than through `align-and-document`: one bullet, already checked.
- The full file list stays on `SurlServerCompositionSeams.ReadStartFile`; the bullet points there so it cannot drift.
- "The diff" means the work's diff; this task file is committed separately.
- Build clean (0 warnings) and fast tests green. `/verify` not run: no `.cs` or project file changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Surl.Api CLAUDE.md says the TLS option files are read through ReadStartFile, as the code does
