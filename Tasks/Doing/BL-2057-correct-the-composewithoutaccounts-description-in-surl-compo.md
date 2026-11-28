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
completed:
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

- [ ] The `ComposeWithoutAccounts` summary says nothing a search for its callers contradicts.
- [ ] The diff changes only `///` lines.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
