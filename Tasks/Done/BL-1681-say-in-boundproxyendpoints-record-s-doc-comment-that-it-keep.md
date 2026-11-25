---
id: BL-1681
title: Say in BoundProxyEndPoints.Record's doc comment that it keeps every bound proxy end point
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-1681 — Say in BoundProxyEndPoints.Record's doc comment that it keeps every bound proxy end point

## Goal

The XML doc comments on `BoundProxyEndPoints` say it keeps the end point of every proxy or SOCKS listener that has a bound port, whether or not the system chose the port.

## Context

Found by BL-1670. `Application/Surl.Composition.UnitLibrary/BoundProxyEndPoints.cs` says `Record` keeps a listen URL's end point when it is a proxy or SOCKS listener "on a port the system chose", but the code ignores only a listen URL that is not a proxy's or has no `BoundPort`, so a listener on a fixed port is recorded too. Documentation only; no behaviour change. ADR-0088 Amendment 11.

## Acceptance criteria

- [x] The `<summary>` of `BoundProxyEndPoints.Record` no longer limits it to a port the system chose, and matches the condition in the method body.

## Notes

- The new summary also says a host that is not an address is kept as `IPAddress.Any`, which
  `AllowedOriginRule.IsProxyEndPoint` matches against every address. The class summary was already true and is unchanged.
- Made in the session, without delegating to `align-and-document`: one doc comment.
- 2026-11-25 verify: build clean (0 warnings), fast tests green. `dotnet format --verify-no-changes` passes for the edited
  file; across the solution it fails on files this task does not touch (filed as BL-1690 for the errors that are not
  `ENDOFLINE`).

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. BoundProxyEndPoints.Record's doc comment says it keeps every proxy or SOCKS end point with a bound port
