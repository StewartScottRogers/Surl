---
id: BL-1327
title: Fix the win-x64 Curl port publish hash mismatch that stops curl-port-phase-7 test jobs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: []
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1327 — Fix the win-x64 Curl port publish hash mismatch that stops curl-port-phase-7 test jobs

## Goal

The `build-curl-port` job of `curl-port-phase-7` passes on win-x64, so the three test jobs (`needs: build-curl-port`) run.

## Context

Run 43982591865 (factory/phase-1, 2026-11-16): step "Publish the Curl port twice and compare the hashes" failed: the win-x64 publish hashes to 3E249136155B1594D42A1E64E853BA071175D95888079FBA23909E16A9FBFA3A but `CurlPortBuilds.json` pins 7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467. linux-x64 and osx-arm64 builds passed. Find why the CI publish differs from the pin (Curl port commit, SDK version, path-dependent output) and either fix the determinism or re-pin by recorded decision (ADR). Blocks BL-1324 (and the linux/win test-job recording tasks).

## Acceptance criteria

- [ ] The win-x64 publish step's hash matches `CurlPortBuilds.json`, or the pin is changed with an ADR saying why.
- [ ] `build-curl-port` passes on win-x64 in a pushed run of the workflow.

## Notes

## Log

- 2026-11-16: Created.
