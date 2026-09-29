---
id: BL-024
title: Decide Surl's hardening for internet-facing use
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-000, BL-001, BL-023]
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-09-28
completed:
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

- [ ] A new ADR, numbered with the next free number, exists in
      `Documentation/Planning/Decisions/`, is marked "Decided by Claude under Stewart's
      delegation", has status Accepted, and is listed in that folder's `README.md` index.
- [ ] The ADR gives a default number, and says whether an option can change it, for:
      maximum concurrent connections (total and per remote address), connection idle
      timeout, maximum exchange duration, time allowed to receive a request head or
      command line (slow-sender defence), maximum request-head or command-line size per
      protocol family, and maximum upload size.
- [ ] The ADR states the defaults for what a server exposes: uploads off or on,
      directory listing off or on, following symbolic links, and dot-files.
- [ ] The ADR states what error text may reveal (never a local path, stack trace or
      exception message to the peer), and how untrusted bytes are rendered in the
      verbose log so they cannot inject terminal control sequences.
- [ ] The ADR states the TLS minimums (protocol versions) consistent with what the
      pinned upstream curl 8.21.0 build negotiates. It gives BL-002's contract any
      constraint it adds.
- [ ] The ADR says what happens when a limit is hit, per protocol family where they
      differ (HTTP status, FTP-style reply code, connection close), and which
      `SurlExitCode` applies, if any.
- [ ] The ADR lists the follow-up implementation tasks it implies, beyond BL-025, by
      project. They are filed by `task-planner` and their IDs are recorded in this
      task's Log.
- [ ] `Documentation/Product/Product-Overview.md` points to the ADR for the security
      scope. `Documentation/Wiki/Glossary.md` has a row for each term it introduces.

## Notes

## Log

- 2026-09-28: Created.
