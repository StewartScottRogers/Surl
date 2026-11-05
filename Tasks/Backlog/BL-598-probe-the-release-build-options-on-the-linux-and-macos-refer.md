---
id: BL-598
title: Probe the release build options on the Linux and macOS reference pins
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-389, BL-392]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, .github/workflows/ci.yml]
requirement: none
created: 2026-11-04
completed:
---
# BL-598 — Probe the release build options on the Linux and macOS reference pins

## Goal

`UpstreamCurlBuildFeatures.ReleaseBuildOptions` and its platform options (`xattr`, `getrlimit`,
`codeset-utf8`, `cert-status`, `sha512-256`, `wakeup`) are confirmed by probe commands run with
the Linux and macOS reference pins, as BL-389 confirmed them for the Windows one.

## Context

- ADR-0080 decision 3 ("Features"): each build option is measured against each reference pin
  before selection relies on it. BL-389 probed the Windows reference pin (its Notes list the probe
  commands); the Linux and macOS pins are not installed on Stewart's machine, so they run where
  BL-392's `Upstream test cases` CI job fetches them.
- Probe with the pinned build only, through `UpstreamCurlLocator`, never the Curl port (ADR-0003).

## Acceptance criteria

- [ ] An `Integration` test in `Surl.Conformance.UnitTests` runs BL-389's probe commands with the
      platform's reference pin and asserts each option `UpstreamCurlBuildFeatures.For` claims.
- [ ] The test passes on the Linux and macOS CI legs; any option a probe disproves is corrected in
      `UpstreamCurlBuildFeatures`, with the probe's output recorded in Notes.

## Notes

## Log

- 2026-11-04: Created.
