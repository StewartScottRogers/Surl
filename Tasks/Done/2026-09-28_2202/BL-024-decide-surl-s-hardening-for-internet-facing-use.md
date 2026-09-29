---
id: BL-024
title: Decide Surl's hardening for internet-facing use
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-000, BL-001, BL-023]
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md, Documentation/Wiki/Glossary.md, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-024 — Decide Surl's hardening for internet-facing use

## Goal

An accepted ADR states the security posture an internet-facing `surl` must hold: every
resource limit, timeout, default and refusal, each with a number or rule that a test can
pin. The command-line ADR (BL-003) and the serving tasks can then build it in, not bolt
it on.

## Context

- Stewart, 2026-09-28: Surl must be hardened for internet-facing use (product overview,
  open question 3, recorded by BL-023). Until then the product overview assumed only
  local testing.
- Fidelity to upstream curl stays the target (product overview, "Non-goals"). A
  hardening limit must never make a legitimate upstream curl 8.21.0 exchange fail. Where
  a limit could, the ADR says how it is set high enough, or configurable.
- Inputs: the listener-seam ADR (BL-000), the exit-code ADR (BL-001), and
  `Surl.Content.UnitLibrary/CLAUDE.md` (no path ever escapes the served root).
- Base class library only. No package for rate limiting or anything else.
- This is a design decision delegated to Claude (root `CLAUDE.md`, "Decisions"). BL-003
  waits on it, so its option table can carry the limits' options.

## Acceptance criteria

- [x] A new ADR, numbered with the next free number, exists in
      `Documentation/Planning/Decisions/`, is marked "Decided by Claude under Stewart's
      delegation", has status Accepted, and is listed in that folder's `README.md` index.
- [x] The ADR gives a default number, and says whether an option can change it, for:
      maximum concurrent connections (total and per remote address), connection idle
      timeout, maximum exchange duration, time allowed to receive a request head or
      command line (slow-sender defence), maximum request-head or command-line size per
      protocol family, and maximum upload size.
- [x] The ADR states the defaults for what a server exposes: uploads off or on,
      directory listing off or on, following symbolic links, and dot-files.
- [x] The ADR states what error text may reveal (never a local path, stack trace or
      exception message to the peer), and how untrusted bytes are rendered in the
      verbose log so they cannot inject terminal control sequences.
- [x] The ADR states the TLS minimums (protocol versions) consistent with what the
      pinned upstream curl 8.21.0 build negotiates. It gives BL-002's contract any
      constraint it adds.
- [x] The ADR says what happens when a limit is hit, per protocol family where they
      differ (HTTP status, FTP-style reply code, connection close), and which
      `SurlExitCode` applies, if any.
- [x] The ADR lists the follow-up implementation tasks it implies, beyond BL-025, by
      project. They are filed by `task-planner` and their IDs are recorded in this
      task's Log.
- [x] `Documentation/Product/Product-Overview.md` points to the ADR for the security
      scope. `Documentation/Wiki/Glossary.md` has a row for each term it introduces.

## Notes

- Decided in ADR-0006 (Decided by Claude under Stewart's delegation). Defaults: 1024
  connections total, 100 per remote address, 120 s idle timeout, 3600 s maximum exchange
  duration, 30 s head timeout, 100 KiB request head, 8 KiB command line, 1 MiB framed
  message, 100 MiB upload; 0 turns any limit off. Uploads, directory listings, symbolic
  links and dot-files all off by default. TLS 1.2 and 1.3 by default.
- Why these numbers: pinned curl's own defaults (`--manual`): `-Z` runs 50 transfers at
  once, so 100 per address fits two default runs; libcurl reuses a cached connection up
  to 118 s, so an idle timeout of 120 s never closes one under it; curl sets no
  `--max-time`, so the hour is configurable and 0 turns it off.
- Measured with the pinned build (SHA-256 `0E773709…8778`): it completes against a
  TLS 1.2 only and a TLS 1.3 only server with its defaults; `--tls-max 1.1` and
  `--tlsv1.3` against a mismatched server exit 35. It reports a 503 with `-f` as exit 22,
  a bare close as exit 52 (`Empty reply from server`), and a 431 as exit 0 with
  `%{http_code}` 431. So refusals answer in the protocol's words.
- Added `Record-CurlExchange.ps1`'s `-TlsProtocol` parameter (`Tls12`, `Tls13`,
  `Tls12AndTls13`; HTTP mode only) to measure TLS versions. Neither BL-004 nor BL-009 in
  Doing names the script, so it is added to this task's `touches` (lane rule 3).
- The planner pointed out two gaps in the first draft, and the ADR was fixed before it
  was accepted. Upstream curl speaks MQTT 3.1.1, which has no server `DISCONNECT`, so an
  MQTT limit hit now closes with no bytes. There was no refusal contract for datagram
  flows, so the ADR adds `IDatagramRefusalWriter`. BL-046, BL-053 and BL-054 were edited
  to match.
- The Phase 2 and later servers take their rows of ADR-0006 in their own serving tasks;
  no task is filed for them yet, because those servers have no tasks.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: ADR-0006 accepted. Follow-up tasks filed by task-planner: BL-046 (Abstractions), BL-047 (Content), BL-048 (Networking), BL-049 (Output), BL-050 (Http), BL-051 (Dict), BL-052 (Gopher), BL-053 (Mqtt), BL-054 (Tftp); BL-025 (Core) already existed.
- 2026-09-28: Doing -> Done. ADR-0006 fixes Surl's internet-facing limits, exposure defaults, peer-visible text and TLS 1.2 minimum; BL-046..BL-054 filed to build it
