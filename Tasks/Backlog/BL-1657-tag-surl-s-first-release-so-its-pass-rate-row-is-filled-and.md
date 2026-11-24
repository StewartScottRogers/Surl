---
id: BL-1657
title: Tag Surl's first release so its pass-rate row is filled and the NuGet packages are published
priority: Normal
assignee: Stewart
pipeline: direct
depends-on: []
touches: []
requirement: none
created: 2026-11-24
completed:
---
# BL-1657 — Tag Surl's first release so its pass-rate row is filled and the NuGet packages are published

## Goal

Stewart decides when to tag Surl's first release; once tagged, the release workflow
publishes the binaries and the `Surly.Surl.Api` and `Surly.Surl.Mcp` packages.

## Context

The roadmap lists these as still open with no task: Milestone 6 ("no release is tagged,
and the release row of the pass-rate file is empty") and the hosts track ("the packages
are packed but not published until the first release tag"); NFR-006 also waits on "a dry
run of the commit to be tagged". A tag and a release are Stewart's to ask for (root
`CLAUDE.md`, "Git and GitHub"). ADR-0187 names the packages.

## Acceptance criteria

- [ ] Stewart has said whether and when to tag, recorded in the Log by his own word.

## Notes

Once tagged, a Claude task fills the pass-rate file's release row and the roadmap and
NFR-006 statements.

## Log

- 2026-11-24: Created.
