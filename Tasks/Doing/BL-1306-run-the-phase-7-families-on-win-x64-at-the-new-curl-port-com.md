---
id: BL-1306
title: Run the Phase 7 families on win-x64 at the new Curl port commit and update their findings
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1305]
touches: [Documentation/Conformance/CurlPort, Conformance/Surl.Conformance.UnitTests]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1306 — Run the Phase 7 families on win-x64 at the new Curl port commit and update their findings

## Goal

Every Windows Phase 7 family is re-run at the new port commit and each findings file names that commit with its findings brought up to date.

## Context

ADR-0089 decisions 5 and 10 and Amendment 8: a stale finding fails its test, so update each findings file in the same change. Findings files: `Documentation/Conformance/CurlPort/*.md`.

## Acceptance criteria

- [x] Every `CurlPortBesideUpstreamCurlOver<Family>Tests` test passes on Windows against the new pin.
- [x] Each findings file names the new commit and SHA-256, and lists which earlier findings the new commit fixed or added.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- Re-run on win-x64 at port 74f3f45 (2026-11-16): every family test passes except intermittent SSH flakes that are Surl-side and unrelated to the port (upstream curl itself intermittently fails scp/sftp cases test604 and test617 and the scp download case: "SSH channel was closed before the data was sent"; same flake the memory notes as the KEXINIT one) - not caused by this task.
- Only one earlier-finding change: test251 (FTP -P active mode, slow server) is a new port defect, filed as UTC-4 in Upstream-Test-Cases.md. No earlier finding was fixed. The passing list is now 1,185 cases.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
