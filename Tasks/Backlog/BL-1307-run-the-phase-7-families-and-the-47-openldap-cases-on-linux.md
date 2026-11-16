---
id: BL-1307
title: Run the Phase 7 families and the 47 OpenLdap cases on linux-x64 and record the linux-x64 findings
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1304, BL-1305, BL-1306]
touches: [.github/workflows/curl-port-phase-7.yml, Documentation/Conformance/CurlPort]
requirement: FR-057
created: 2026-11-16
completed:
---
# BL-1307 — Run the Phase 7 families and the 47 OpenLdap cases on linux-x64 and record the linux-x64 findings

## Goal

The `curl-port-phase-7` workflow runs the Phase 7 families, including the 47 `OpenLdap_` cases, on linux-x64 and the result is recorded in a linux-x64 section of each family file.

## Context

ADR-0089 decision 8 and Amendment 8. Adds the test step to the workflow of BL-1304 with the upstream builds from `.github/actions/upstream-curl` (openldap: true).

## Acceptance criteria

- [ ] The workflow has a linux-x64 test job running the Phase 7 families and the `OpenLdap_` cases after the build, and a run of it completed.
- [ ] Each family file has a linux-x64 section naming the commit and findings.
- [ ] The 47 `OpenLdap_` cases are no longer Inconclusive on linux-x64.

## Notes

## Log

- 2026-11-16: Created.
