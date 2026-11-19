---
id: BL-1409
title: Align ADR-0114, ADR-0116, ADR-0117 and ADR-0119 with how the extended servers now judge a login with no account
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1397, BL-1398, BL-1400]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1409 — Align ADR-0114, ADR-0116, ADR-0117 and ADR-0119 with how the extended servers now judge a login with no account

## Goal

ADR-0116 decision 7, ADR-0117 decision 7 and ADR-0119 decision 5 say each call, `CONNECT` or request is judged with
`IsWrite: true`, so a server with no account refuses a caller without a login unless `--allow-anonymous`; and
ADR-0114 decision 3 says a login accepted unchecked under `--allow-anonymous` binds to the authcid it named.

## Context

- The code is fixed elsewhere; the ADRs still describe the old argument, which is how a later reader comes to
  believe something false (root `CLAUDE.md`, "Say what it does, do what it says").
  - ADR-0116 decision 7 (line 249): `HttpAuthenticationRequest("POST", <:path>, IsWrite: false, <regular fields>)`;
    BL-1397 makes it `true`.
  - ADR-0117 decision 7 (line 275): `HttpAuthenticationRequest("CONNECT", <:path>, IsWrite: false, ...)`; BL-1398
    makes it `true`.
  - ADR-0119 decision 5 (line 237): `HttpAuthenticationRequest(<method>, <path>, IsWrite: false, <fields>)`;
    BL-1091 made it `true` on 2026-11-12 (commit 6b1fc29be, `SignalRRequestAnswerer.cs:55`) without amending the ADR.
  - ADR-0114 decision 3 gives the `anon-` JID "for `ANONYMOUS`" only; BL-1400 binds an unchecked named login to its
    authcid, which the decision did not spell out.
- Why `true`: `Foundation/Surl.Authentication.UnitLibrary/HttpAuthenticationSession.cs:180` lets a non-write proceed
  anonymously when no account exists; that is ADR-0032 section 4's HTTP read rule, and ADR-0032's Context items 1
  and 2 keep anonymous access to HTTP, Gopher and TFTP. Source: Surly cookbook bug report 2026-11-19 09:30,
  findings 1, 2 and 5.
- Each amendment is a short section at the end of its ADR, "Decided by Claude under Stewart's delegation", naming
  the task that changed the code; the decision text itself is corrected where it names the argument.

## Acceptance criteria

- [x] ADR-0116, ADR-0117 and ADR-0119 each name `IsWrite: true` in the decision text, and each gains an amendment
      section saying why (ADR-0032 Context items 1 and 2) and which task changed the code (BL-1397, BL-1398,
      BL-1091).
- [x] ADR-0114 gains an amendment section stating the unchecked-login JID rule as BL-1400 built it.
- [x] `Documentation/Conformance/Extended/Grpc.md`, `WebTransport.md` and `SignalR.md`, where a row states the
      no-account behaviour, agree with the ADRs (or the task Log says none does).
- [x] No `.cs` file changes.

## Notes

- The title says "now": take it only after the three code tasks it depends on are Done, and quote their commits.


## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Doing -> Done. ADR-0114, 0116, 0117, 0119 amended; WebTransport.md no-account row corrected; Grpc.md and SignalR.md already agree
