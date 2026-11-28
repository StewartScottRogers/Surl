---
id: BL-2057
title: Correct the ComposeWithoutAccounts description in Surl.Composition
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-28
---
# BL-2057 — Correct the ComposeWithoutAccounts description in Surl.Composition

## Goal

`AuthenticationComposition.ComposeWithoutAccounts`'s XML description is true of what calls it.

## Context

Found while working BL-2014 (2026-11-27), read from source. Documentation only, in
`Application/Surl.Composition.UnitLibrary/AuthenticationComposition.cs`. The summary says the method
builds "what the servers are composed with when only their schemes are wanted". That path calls
`SurlServerComposition.ComposePolicyWithoutAccounts`; a search on 2026-11-27 found the only caller of
`ComposeWithoutAccounts` in `Application/Surl.Console.UnitTests/CommandLineRunnerLdapTests.cs`.

## Acceptance criteria

- [x] The `ComposeWithoutAccounts` summary says nothing a search for its callers contradicts.
- [x] The diff changes only `///` lines.
- [x] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean.

## Notes

- 2026-11-28: A search of every `.cs` file found one caller, `CommandLineRunnerLdapTests`
  (`LoadLdapServerAsync_MalformedFile_...`). The summary now says what the method builds, names no
  caller of it, and points to `ComposePolicyWithoutAccounts` for the scheme-listing path.
- Edited directly rather than through `align-and-document`: one doc comment in one file.
- The library's `CLAUDE.md` already names `ComposePolicyWithoutAccounts` for that path, so it needed no change.
- Verify: build `-warnaserror` 0 warnings, format check clean, fast tests 30681 passed, 3 skipped.
  `Integration` tests not run: only `///` lines changed.

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The ComposeWithoutAccounts summary describes the policy it builds and points to ComposePolicyWithoutAccounts for scheme listing.
