---
id: BL-1919
title: Correct the data directory lock seam comment and the ComposeAuthentication file list comment
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1881]
touches: [Application/Surl.Composition.UnitLibrary/SurlServerCompositionSeams.cs, Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1919 — Correct the data directory lock seam comment and the ComposeAuthentication file list comment

## Goal

The `TakeDataDirectoryLock` and `ComposeAuthentication` comments in `Surl.Composition` say when the lock seam is called and which start files are read, as the code does.

## Context

Found while working BL-1882 (2026-11-27), read from source. Comment text only; no behaviour change. BL-1881 edits both files (other comments), so this task follows it.

- `Application/Surl.Composition.UnitLibrary/SurlServerCompositionSeams.cs`, `<param name="TakeDataDirectoryLock">`: says it is never called without `--directory`. `TakeEveryLock` also calls it for each `--share` path that is not read-only.
- `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs`, the comment above `ComposeAuthentication`: lists some of the files it reads. It reads each `--mailbox-file`, the `--user-file`, each `--authorized-keys` file, the `--keytab` file, the `--user-ldap` `bind-password-file` and `ca-file`, the `--proxy-user-file`, each `--hostkey` then `--hostcert` file, and the `--control-token-file`, in that order.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] The `TakeDataDirectoryLock` comment names both `--directory` and each writable `--share` path, as `TakeEveryLock` does.
- [ ] The `ComposeAuthentication` comment names every file the method reads, in the order read, or names none.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean and the diff of each `.cs` file changes comment lines only.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
