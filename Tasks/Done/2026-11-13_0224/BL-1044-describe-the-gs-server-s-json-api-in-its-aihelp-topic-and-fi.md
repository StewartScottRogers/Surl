---
id: BL-1044
title: Describe the gs server's JSON API in its aihelp topic and finish its matrix
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1026, BL-1045, BL-1041, BL-1042, BL-1043]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Conformance/Extended/GoogleCloudStorage-Json.md]
requirement: none
created: 2026-11-11
completed: 2026-11-13
---
# BL-1044 — Describe the gs server's JSON API in its aihelp topic and finish its matrix

## Goal

`surl --aihelp gs` says the server also answers the JSON API on the same listener, and every row of the JSON matrix names a test or says why none is needed.

## Context

- Decision: ADR-0148 (about text and example for `STORAGE_EMULATOR_HOST`); BL-1026 adds the `gs` topic, whose ADR-0110 decision 8 text names the XML API only.
- `AiHelpProse.TopicAbout` and `AiHelpExamples` change with the behaviour in the same diff (root `CLAUDE.md`).

## Acceptance criteria

- [x] The `gs` about text names the JSON API, Bearer-only authentication on it, and `501` for what is not served; the example is checked by `CommandLineRunnerAiHelpTests`.
- [x] Every "covered by" cell of `GoogleCloudStorage-Json.md` names a passing test or `n/a: <reason>`.
- [x] Build and fast tests green.

## Notes

- The example is the existing --gs-plaintext one (the STORAGE_EMULATOR_HOST case); its description now names both APIs. Matrix intro and stale task names corrected.

## Log

- 2026-11-11: Created.
- 2026-11-13: Backlog -> Doing.
- 2026-11-13: Doing -> Done. gs about text names the JSON API; matrix complete
