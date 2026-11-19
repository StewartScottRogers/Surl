---
id: BL-1380
title: Take the data-directory lock on every --share path
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-18
completed:
---
# BL-1380 — Take the data-directory lock on every --share path

## Goal

A `surl` that serves a writable `--share <name>=<path>` holds `<path>/.surl/lock` as `--directory <path>` does, so
a second `surl` given the same folder as `--directory` or as a writable share - in either order - is refused with
exit 124 before any listener binds.

## Context

- Finding 4 of the Surly cookbook bug report of 2026-11-18 (reported on `a0236e175`, not re-run by the reporter):
  `surl --directory site --allow-uploads --allow-anonymous http://127.0.0.1:8080/` and then
  `surl --share keep=site --allow-uploads --allow-anonymous http://127.0.0.1:8081/` both start, and an upload to
  `http://127.0.0.1:8081/keep/viashare.txt` is served by the first. A second `--directory site` is refused today
  with `surl: (124) Directory site is in use by another surl process`.
- The lock: `DataDirectoryLock.Take` (`Application/Surl.Composition.UnitLibrary/DataDirectoryLock.cs:32-58`,
  ADR-0031 decision 7) opens `<path>/.surl/lock` with `FileShare.None`; `DataDirectoryLockOutcome.InUse`
  (`DataDirectoryLockOutcome.cs:47-50`) is exit 124 and the text above. It is taken only for `--directory`, in
  `SurlServerComposition.LockThenComposeAsync` (`SurlServerComposition.Start.cs:282-315`), through the seam
  `SurlServerCompositionSeams.TakeDataDirectoryLock` (which `Surl.Api` also supplies,
  `Application/Surl.Api/SurlServerHostSeams.cs:29`), after the shares are composed by `ContentShareComposition`.
  Within one process an overlap between `--directory` and a share is already refused `(2) option --share: share
  <name> overlaps <other>`.
- The rule to build: after the `--directory` lock, take the same lock, through the same seam, on the path of every
  share that is not `;readonly` (`ContentShare.IsReadOnly`, `Foundation/Surl.Content.UnitLibrary/ContentShare.cs:48`),
  in command-line order. A read-only share writes nothing, so it takes no lock and starts even on a read-only
  folder. The first refusal ends the start with its exit code (124, or 23 when `.surl` cannot be created) and
  its text naming the share's path as given; every lock taken before it is released; every lock is handed to the
  composed serving and released when it is disposed, as the data-directory lock is.
- The `.surl` folder this creates inside a share is already hidden: `ContentStore` hides `.surl` as the first
  segment under every container's own root (`Foundation/Surl.Content.UnitLibrary/ContentStore.cs:341-351`,
  `1208-1221`), so `/keep/.surl/lock` is never served.
- Stand-in: no store or event changes; a test that runs several `surl` processes over one folder gets the same
  one-writer guarantee whichever option names it. `Surl.Api` gets it through the shared composition.

## Acceptance criteria

- [ ] A test in `SurlServerCompositionTests` (over `StartTestSeams`) shows a start with `--share keep=site` whose
      lock seam answers `InUse` fails with `SurlExitCode.DataDirectoryInUse` and the line
      `(124) Directory site is in use by another surl process`, with no listener bound.
- [ ] A test shows a `;readonly` share asks the seam for no lock.
- [ ] A test shows that when a share's lock is refused, the `--directory` lock and every share lock taken before it
      are disposed, and another shows every lock is disposed with the `ComposedServing`.
- [ ] An `[TestCategory("Integration")]` test using the real `DataDirectoryLock.Take` on a temporary folder shows a
      start holding the folder as a writable share refuses a `--directory` start on it with 124, and the reverse.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests are green, and
      `Surl.Composition.UnitLibrary` stays at 100% line and branch coverage.

## Notes

- The document stage adds an amendment to ADR-0031 decision 7 stating that writable shares are locked too and why
  read-only ones are not, and updates `Application/Surl.Composition.UnitLibrary/CLAUDE.md`'s start-order table and
  `DataDirectoryLock` bullet. The `--aihelp` text is BL-1381.
- Nested folders across processes (one `surl` on `site`, another on `site/sub`) stay unguarded, as they are for two
  `--directory` values today; do not widen this task to cover them.

## Log

- 2026-11-18: Created.
