---
id: BL-1625
title: Triage the osx-arm64 curl-port-phase-7 failures of BL-1624's run into each family's findings table
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-1624]
touches: [Documentation/Conformance/CurlPort, Conformance/Surl.Conformance.UnitTests]
requirement: FR-057
created: 2026-11-24
completed:
---
# BL-1625 — Triage the osx-arm64 curl-port-phase-7 failures of BL-1624's run into each family's findings table

## Goal

Every osx-arm64 failure of the `curl-port-phase-7` run BL-1624 recorded is classified, with pinned upstream curl as the oracle, as a Curl port defect, a Surl defect or a test defect, and each port defect is listed under a `### Findings (osx-arm64)` heading in its family file, so a rerun on osx-arm64 fails no case its family file does not list.

## Context

This is the osx-arm64 twin of BL-1343 (linux-x64, archived in `Tasks/Done/2026-11-17_1037/`); read its Notes for the method. The run ID is in BL-1624's Notes; read its `phase-7-osx-arm64-results` artifact (trx) for each failure's verdict and log. A failure reads "A port defect Documentation/Conformance/CurlPort/<Family>.md does not list" (unlisted defect) or "lists …, but the port now agrees with upstream curl" (stale listing). A heading naming a platform in parentheses scopes its rows to that platform (`CurlPortFindingsFile.LinesForPlatform`), so osx-arm64 rows go under `### Findings (osx-arm64)`, and a win-x64 or linux-x64 finding that does not show on osx-arm64 must be scoped so it does not fail there.

Upstream curl on osx-arm64 is the reference build `04E0E69B…EEBF` (curl 8.21.0, `aarch64-apple-darwin`, OpenSSL 4.0.1). A case where upstream curl itself did not complete as its mirrored test asserts is not a port defect; say what it is (test defect fixed here, or a Surl defect filed as its own task). A test defect fix stays inside `Conformance/Surl.Conformance.UnitTests` (an `[OSCondition]`, a platform-scoped expectation). Causes inside the port are not traced. Never judge by the Curl port (ADR-0003); never write to the port's repository. New findings reach `Port-Board-Handoff.md` through BL-1628, not here.

If the failures span more than about six families or 60 cases, triage the largest families here and file the rest as one follow-up task per family, listed in Notes.

## Acceptance criteria

- [ ] Each osx-arm64 failure of BL-1624's run is either listed under its family file's `### Findings (osx-arm64)` with a finding ID and a one-line description, or fixed as a test defect, or filed as a Surl defect task; the Notes give the count of each.
- [ ] Each new finding has its own `####` subsection in its family file, in the form the existing linux-x64 findings (for example `Ldap.md` L-1) use.
- [ ] Each family file's `## osx-arm64` section says it was triaged by BL-1625 and what was added.
- [ ] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests (`TestCategory!=Integration`), including `CurlPortFindingsFileTests`, pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
