---
id: BL-1125
title: Say in the --control help text that surl serves the control endpoint
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-14
completed:
---
# BL-1125 — Say in the --control help text that surl serves the control endpoint

## Goal

The `--control` option help no longer says this build does not serve it.

## Context

`CommandLineOptions.cs` (the `--control` description) says "this build reads and checks it but does not serve it yet", but `ComposedServing` binds it since BL-977. Fix the text and any test or `--aihelp` page that pins the old words.

## Acceptance criteria

- [ ] The `--control` help says surl serves the endpoint; build and fast tests are green.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
