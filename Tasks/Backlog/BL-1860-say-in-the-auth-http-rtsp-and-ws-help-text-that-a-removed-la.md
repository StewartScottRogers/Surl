---
id: BL-1860
title: Say in the auth, http, rtsp and ws help text that a removed last account leaves the login required
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1855]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1860 — Say in the auth, http, rtsp and ws help text that a removed last account leaves the login required

## Goal

`surl --aihelp` and the manual say that "no account configured" opens reads only for a scheme that never had an
account, so removing the last one while the server runs opens nothing.

## Context

- ADR-0132 amendment 5, decisions 1 and 2.
- `AiHelpProse` paragraphs beginning "Unless `--allow-anonymous` is given: with no account configured" (auth),
  "The login is judged before the method" (http), "Requests are judged as HTTP requests are" (RTSP) and "The upgrade
  is judged as an HTTP GET" (WebSocket); `ManualText`'s copy of the auth paragraph.

## Acceptance criteria

- [ ] The auth paragraph and its `ManualText` copy say that once a scheme has had an account to log in to, removing
      the last one through `--control` leaves the login required until a reset or restore returns a state without one.
- [ ] The http, RTSP and WebSocket paragraphs say, or point to the auth topic for, the same rule, and none says that
      an account removed while the server runs opens reads.
- [ ] `ManualTextTests` and the `AiHelpTextTests` pass.

## Notes

## Log

- 2026-11-27: Created.
