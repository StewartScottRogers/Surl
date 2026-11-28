---
id: BL-2091
title: Say in the surl LDAP help and manual text that the --control endpoint can add directory entries
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-049
created: 2026-11-28
completed:
---
# BL-2091 — Say in the surl LDAP help and manual text that the --control endpoint can add directory entries

## Goal

`surl --aihelp` and the manual text no longer say the LDAP directory is read-only or stays empty without saying that `--control` can add entries while surl runs.

## Context

Found while working BL-2075 (2026-11-28). The statements, in `Application/Surl.Cli.UnitLibrary`:

- `AiHelpProse.cs` line 96, the served-root paragraph: "without it, ... the directory is empty".
- `AiHelpProse.cs` line 330, the `ldap` topic: searches are answered "from one read-only directory".
- `AiHelpProse.cs` line 331: "Without `--directory` the directory is empty; ... a missing file is an empty directory".
- `ManualText.cs` lines 137-139: "a missing file is an empty directory ... Without --directory, the directory is empty." `Application/Surl.Cli.UnitTests/ManualTextTests.cs` lines 146-148 pin this text.
- `ManualText.cs` line 440: searches are answered "from one read-only directory", pinned by `ManualTextTests.cs` line 449.

What is true of `surl`: the command line has no LDAP start-time seed (ADR-0158 decision 3), so without `--directory`, or without the file, the directory holds no entries at start. With `--control`, `POST /v1/directory` adds LDIF entries in memory while surl runs (`ComposedControlTarget.SeedEntriesAsync` in `Surl.Composition`). LDAP clients' add, delete, modify and rename are still refused `unwillingToPerform`, and the file is never written. The Glossary's "directory file" row in `Documentation/Wiki/Glossary.md` shows the wording.

Stand-in: text only; the directory store, its seed, reset, events and faults are unchanged.

## Acceptance criteria

- [ ] Both `AiHelpProse` paragraphs that speak of an empty directory (served root and `ldap` topic) say it is empty at start and that the `--control` endpoint's `POST /v1/directory` can add entries.
- [ ] The `ldap` topic says it is LDAP clients that cannot change the directory instead of calling it read-only without that limit.
- [ ] The LDAP paragraph of `ManualText` at lines 135-139 says the same about the start state and `--control`, and its line 440 says it is LDAP clients that cannot change the directory.
- [ ] `ManualTextTests` pins the new manual text and passes.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
