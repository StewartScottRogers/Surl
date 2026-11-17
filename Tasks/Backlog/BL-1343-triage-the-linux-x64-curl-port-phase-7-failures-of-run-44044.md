---
id: BL-1343
title: Triage the linux-x64 curl-port-phase-7 failures of run 44044472038 into each family's findings table
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Documentation/Conformance/CurlPort, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1343 — Triage the linux-x64 curl-port-phase-7 failures of run 44044472038 into each family's findings table

## Goal

Every linux-x64 failure of `curl-port-phase-7` run 44044472038 is classified, with upstream curl as the oracle, as a Curl port defect, a Surl defect or a test defect, and listed in its family's findings table.

## Context

BL-1339 made linux-x64 compare instead of throw `UnpinnedCurlPortClientException`. Run 44044472038 (`factory/phase-1`, a5337bdf7): 371 passed, 71 failed, 47 not executed. Failures by class: Ftp 4, Http3 1, Ldap 47 (27 `OpenLdap_`, 20 `WinLdap_`), Mail 13, Ssh 5, UpstreamTestCases 1. The assertion text is "A port defect Documentation/Conformance/CurlPort/<Family>.md does not list". Artifact: `phase-7-linux-x64-results`. The 20 `WinLdap_` failures may need an OS condition rather than a finding. Judge by pinned upstream curl, never by the Curl port (ADR-0003).

## Acceptance criteria

- [ ] Each of the 71 failures is either listed in its family file's findings table with its classification, or fixed in Surl.
- [ ] The `WinLdap_` cases do not fail on linux-x64 (OS condition or finding).
- [ ] A rerun of `curl-port-phase-7` on linux-x64 fails no case that its family file does not list.

## Notes

## Log

- 2026-11-17: Created.
