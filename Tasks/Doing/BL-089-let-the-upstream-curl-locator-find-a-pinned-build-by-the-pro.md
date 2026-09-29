---
id: BL-089
title: Let the upstream curl locator find a pinned build by the protocol it needs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-089 — Let the upstream curl locator find a pinned build by the protocol it needs

## Goal

A conformance test can ask `UpstreamCurlLocator` for the pinned build of a platform whose
`protocols` include the scheme it measures, so SMB on `win-x64` finds the static-curl
8.21.0 build rather than the curl.se 8.22.0 one.

## Context

- ADR-0030 pins stunnel/static-curl's 8.21.0 Windows build as a second `win-x64`
  supplementary build, for SMB only, listed after ADR-0017's 8.22.0 build.
- `Surl.Conformance.UnitLibrary/UpstreamCurlLocator.cs`, `Locate(pins, platform, role)`,
  returns the first verified build of the platform and role, so today it returns the
  8.22.0 build, which has no `smb`/`smbs`.
- `PinnedUpstreamCurlBuild.Protocols` already carries each pin's protocol list.
- Rule (ADR-0017 decision 3, ADR-0030 decision 1): the reference build answers every case
  it can; a supplementary build is used only for what its ADR names.

## Acceptance criteria

- [ ] `UpstreamCurlLocator` has a way to locate by scheme: it returns the platform's
      reference build when that build's `Protocols` contains the scheme, and otherwise the
      first verified supplementary build whose `Protocols` contains it.
- [ ] With the real `UpstreamCurlBuilds.json`, locating `smb` on `win-x64` selects the entry
      whose `defaultPath` is `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`,
      and locating `http` selects the Git for Windows reference build (unit tests through
      `IUpstreamCurlFileAccess`, no disk).
- [ ] No pinned build of the platform with the scheme gives a location that says so, as
      `NoPinnedBuild` does for a role.
- [ ] Existing `Locate(pins, platform, role)` callers and tests are unchanged and pass;
      `Surl.Conformance.UnitLibrary` stays at 100% line and branch coverage.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
