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
completed:
---
# BL-1681 — Say in BoundProxyEndPoints.Record's doc comment that it keeps every bound proxy end point

## Goal

The XML doc comments on `BoundProxyEndPoints` say it keeps the end point of every proxy or SOCKS listener that has a bound port, whether or not the system chose the port.

## Context

Found by BL-1670. `Application/Surl.Composition.UnitLibrary/BoundProxyEndPoints.cs` says `Record` keeps a listen URL's end point when it is a proxy or SOCKS listener "on a port the system chose", but the code ignores only a listen URL that is not a proxy's or has no `BoundPort`, so a listener on a fixed port is recorded too. Documentation only; no behaviour change. ADR-0088 Amendment 11.

## Acceptance criteria

- [ ] The `<summary>` of `BoundProxyEndPoints.Record` no longer limits it to a port the system chose, and matches the condition in the method body.

## Notes

## Log

- 2026-11-24: Created.
