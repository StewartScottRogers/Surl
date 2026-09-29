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
completed: 2026-09-29
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

- [x] `UpstreamCurlLocator` has a way to locate by scheme: it returns the platform's
      reference build when that build's `Protocols` contains the scheme, and otherwise the
      first verified supplementary build whose `Protocols` contains it.
- [x] With the real `UpstreamCurlBuilds.json`, locating `smb` on `win-x64` selects the entry
      whose `defaultPath` is `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`,
      and locating `http` selects the Git for Windows reference build (unit tests through
      `IUpstreamCurlFileAccess`, no disk).
- [x] No pinned build of the platform with the scheme gives a location that says so, as
      `NoPinnedBuild` does for a role.
- [x] Existing `Locate(pins, platform, role)` callers and tests are unchanged and pass;
      `Surl.Conformance.UnitLibrary` stays at 100% line and branch coverage.

## Notes

- Added `UpstreamCurlLocator.LocateForProtocol(pins, platform, protocol)` beside the
  unchanged `Locate(pins, platform, role)`. It keeps the platform's pins whose `Protocols`
  contain the protocol (case-insensitive), takes the reference ones if any, otherwise the
  supplementary ones in file order, and verifies them with the existing
  `VerifyFirstPresent`. No pin supports it: `UpstreamCurlLocation.NoPinnedBuildForProtocol`,
  message "UpstreamCurlBuilds.json pins no upstream curl build for <platform> that supports
  <protocol>.", reusing `UpstreamCurlUnavailability.NoPinnedBuildForPlatform` (its doc now
  says "role, or protocol") rather than adding an enum value no caller distinguishes.
- Choice: when the reference build supports the protocol but its file is absent, the result
  is `PinnedBuildFileAbsent` for the reference build, not a fall back to a supplementary
  build. ADR-0017 decision 3 says the reference build answers every case it can, so a
  missing reference install must show up as inconclusive, never silently switch builds. No
  new ADR: this applies ADR-0017/ADR-0030 as written.
- The real-pin-file test reads `UpstreamCurlBuilds.json`, puts a distinct stand-in file at
  every pin's default path through `FakeUpstreamCurlFileAccess` and re-pins each to its
  stand-in's hash, so selection follows the real platforms, roles, protocols and order with
  no curl on disk: `smb`/`smbs` -> static-curl 8.21.0, `http` -> Git for Windows.
- Delivered directly rather than through the full `/feature` agent chain: one method and
  one factory in one library; gates run (build, fast tests 2,069 green, format, coverage
  100% line and branch for `Surl.Conformance.UnitLibrary`).
- Follow-up not filed: switching `PinnedUpstreamCurl` (tests) to `LocateForProtocol` is for
  whichever task adds the first SMB conformance test.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. UpstreamCurlLocator.LocateForProtocol finds the pinned build for a scheme: smb on win-x64 selects static-curl 8.21.0, http the Git for Windows reference build
