---
id: BL-1798
title: Correct the release statements in the installers, the requirements and the Surl.Mcp README
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [install.sh, install.ps1, Documentation/Product/Requirements.md, Application/Surl.Mcp/README.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1798 — Correct the release statements in the installers, the requirements and the Surl.Mcp README

## Goal

The installer headers, the requirements and the `Surl.Mcp` package README say what is true of
releases and of nuget.org.

## Context

Found by BL-1790, whose `touches` did not name these files. `install.sh` (header, about line 12)
and `install.ps1` (about line 10) say no release has been published; releases are published
(latest `v0.1.11` on 2026-11-26). `Documentation/Product/Requirements.md` (about lines 90 and
141) says no release is tagged. `Application/Surl.Mcp/README.md` presents `dnx Surly.Surl.Mcp`
and `dotnet tool install` as working, but nuget.org held no version on 2026-11-26 (BL-1797);
check the feed again when this runs. That README is packed into the package.

## Acceptance criteria

- [ ] None of the four files says no release is published or tagged while a release exists.
- [ ] Every statement about the packages on nuget.org matches what the feed holds when the task
      runs.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
