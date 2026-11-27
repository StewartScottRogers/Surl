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
completed:
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

- [ ] `Application/Surl.Api/CLAUDE.md` no longer says any TLS option file is read from disk past the `ReadStartFile` seam.
- [ ] The diff changes `Application/Surl.Api/CLAUDE.md` only.

## Notes

## Log

- 2026-11-27: Created.
