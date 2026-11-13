---
id: BL-1072
title: Compose the http-ipv6 server half so the upstream cases naming it run
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1072 — Compose the http-ipv6 server half so the upstream cases naming it run

## Goal

The upstream test run plays the cases whose `<server>` names `http-ipv6` (HTTP on `[::1]`, `%HTTP6PORT`) instead of
skipping them `server-half-not-composed:http-ipv6`.

## Context

On the Windows run after BL-556, 10 cases skip `server-half-not-composed:http-ipv6`, among them 1230, which also
names `http-proxy` and uses `-x %HOSTIP:%HTTPPORT`. `UpstreamTestCaseServers` already lists `http-ipv6` as built (the
protocol library plays it); only the test composition in `UpstreamTestCaseServerHalves` lacks a listener on `[::1]`
and `ServerFamilies` lacks the name. The FTP, Gopher and mail halves already bind both loopback addresses.

## Acceptance criteria

- [x] `UpstreamTestCaseServerHalves` binds the plaintext HTTP half on `[::1]` and the run gives `%HTTP6PORT` its port.
- [x] `http-ipv6` is in the run's `ServerFamilies`; the cases that named it run on each reference pin and the
      result lines list each by number.
- [x] Newly passing cases are added to the platform's passing list and the pass-rate file; failing cases are grouped
      by cause into new tasks with their IDs in Notes.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Plaintext HTTP half now also binds [::1] (%HTTP6PORT); http-ipv6 added to ServerFamilies. Windows run: 1,152 runnable, 1,144 passed, 8 failed (pre-existing causes, none newly failing), 861 skipped. 14 cases newly pass and are listed. No new failures to file.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. http-ipv6 server half composed on [::1]; 14 more upstream cases pass on win-x64 (1,144)
