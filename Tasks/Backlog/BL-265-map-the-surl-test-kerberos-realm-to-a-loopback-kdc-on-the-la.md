---
id: BL-265
title: Map the SURL.TEST Kerberos realm to a loopback KDC on the lane machine
priority: Normal
assignee: Stewart
pipeline: direct
depends-on: []
touches: []
requirement: FR-046
created: 2026-09-30
completed:
---
# BL-265 — Map the SURL.TEST Kerberos realm to a loopback KDC on the lane machine

## Goal

The lane machine's Windows Kerberos package sends requests for realm `SURL.TEST` to a KDC on
`127.0.0.1`, and maps hosts under `.surl.test` to that realm, so pinned upstream curl can get a
ticket from the hand-built test KDC.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0065-kerberos-logins-are-proved-against-pinned-upstream-curl-through-a-hand-built-loopback-kdc.md` decision 2.
- The change needs administrator rights and changes the machine, so a lane must not make it.
- Run once, from an elevated prompt:
  ```
  ksetup /addkdc SURL.TEST 127.0.0.1
  ksetup /addhosttorealmmap .surl.test SURL.TEST
  ```
- Undo with `ksetup /delhosttorealmmap .surl.test SURL.TEST` and `ksetup /delkdc SURL.TEST 127.0.0.1`.
- Harmless while no test runs: a realm nobody else uses, hosts under the reserved `.test` domain,
  the KDC on loopback. Sign out and in (or reboot) if a later test still finds no KDC.

## Acceptance criteria

- [ ] `ksetup` (run without arguments) lists realm `SURL.TEST` with KDC `127.0.0.1`.
- [ ] `Test-Path HKLM:\SYSTEM\CurrentControlSet\Control\Lsa\Kerberos\Domains\SURL.TEST` is `True`.

## Notes

- Filed by BL-242 (ADR-0065). BL-268 and BL-269 wait on it.

## Log

- 2026-09-30: Created.
