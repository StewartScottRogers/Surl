---
id: BL-1073
title: Pass the TimeProvider to ContentStore as sweepClock when serving --directory
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-874]
touches: [Application/Surl.Composition]
requirement: none
created: 2026-11-12
completed:
---
# BL-1073 — Pass the TimeProvider to ContentStore as sweepClock when serving --directory

## Goal

A `surl --directory` start sweeps abandoned `.surl-upload-` files by passing its `TimeProvider` as `ContentStore`'s `sweepClock`.

## Context

ADR-0150 (BL-874) added the opt-in `sweepClock` argument; nothing passes it yet. Find where `Surl.Composition` constructs `ContentStore` for a disk root.

## Acceptance criteria

- [ ] A test in `Surl.Composition` shows an old `.surl-upload-` file removed when the store is built over a disk root, and the in-memory store sweeps nothing.

## Notes

## Log

- 2026-11-12: Created.
