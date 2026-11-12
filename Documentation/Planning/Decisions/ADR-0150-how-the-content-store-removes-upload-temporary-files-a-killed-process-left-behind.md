# ADR-0150 — How the content store removes upload temporary files a killed process left behind

- **Status:** Accepted
- **Date:** 2026-11-12
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-874.
- **Amends:** [ADR-0130](ADR-0130-how-the-oci-registry-stages-uploads-and-rewrites-its-index.md)
  decision 1 (staging in `ContentStore` upload sessions).

## Context

`ContentStore` writes every upload and upload session to a hidden `.surl-upload-<guid>` file beside
its target and renames it over the target. A clean shutdown or a disposed session deletes the file;
a killed process does not. The OCI registry, S3, Azure and GCS all stage through it, so the files pile
up in a `--directory` folder, and ADR-0115's start-up sweep of `oci-` staging folders has nothing to
sweep. The sweep must never touch a file another live process may be writing.

## Decisions

1. **When.** `ContentStore.RemoveAbandonedUploadFiles(TimeProvider)` does the sweep, and the
   constructor calls it first when it is given a `sweepClock`. No clock, no sweep: the in-memory store
   and every existing caller behave as before. A host serving `--directory` passes its `TimeProvider`.
2. **Age bound.** A file is abandoned when its last write time is more than 24 hours before now on the
   injected `TimeProvider` (`ContentStore.AbandonedUploadAge`). An upload writes continuously, so a
   file untouched for a day belongs to no live upload; a session left idle that long is treated as dead.
3. **What it removes.** A file named `.surl-upload-` and exactly 32 hexadecimal digits, in the served
   root and in every share that takes writes, at any depth.
4. **What it never removes.** A younger file (24 hours exactly is kept); a name of any other shape;
   a directory; anything in a `readonly` share; anything under the top-level `.surl` folder;
   anything reached through a symbolic link to a directory (the walk follows a folder only when its
   final path is the path walked, so it cannot leave the served root); a file the file system
   refuses to delete or to examine (an open file on Windows) - skipped, never thrown.
5. **Why not a live-process lock.** The BCL has no portable "is anyone writing this" test; age on the
   injected clock is deterministic and testable, and the one-day bound errs towards keeping files.

## Consequences

- Hosts must pass a clock to benefit; wiring it in `Surl.Composition` is a separate task.
- A random-access session idle for over a day on Linux or macOS, where deleting an open file
  succeeds, would lose its temporary file; this is accepted.
