---
id: BL-1860
title: Say in the auth, http, rtsp and ws help text that a removed last account leaves the login required
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1863]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
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

- [x] The auth paragraph and its `ManualText` copy say that once a scheme has had an account to log in to, removing
      the last one through `--control` leaves the login required until a reset or restore returns a state without one.
- [x] The http, RTSP and WebSocket paragraphs say, or point to the auth topic for, the same rule, and none says that
      an account removed while the server runs opens reads.
- [x] `ManualTextTests` and the `AiHelpTextTests` pass.

## Notes

- The auth paragraph (`AiHelpProse`) and its `ManualText` copy gained one sentence; the http, RTSP and WebSocket
  paragraphs now state the rule where they said "no account". "A state without one" is worded "a state in which it
  never had one", because a restore of a snapshot taken after the removal holds no account yet keeps the login
  (ADR-0132 amendment 5, decision 2).
- The text names `--control` only: it is how a run started from the command line removes an account.
- `ManualTextTests` pins the new manual lines. Verified: build clean, `dotnet format --verify-no-changes` passes,
  all fast tests pass (`Surl.Cli.UnitTests` 1989 of 1989). No integration tests run: only help text changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The auth, http, rtsp and ws --aihelp text and the manual say a scheme that has had an account keeps asking for a login after the last one is removed through --control
