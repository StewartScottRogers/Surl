---
id: BL-1651
title: Fix stale built-or-not statements in the product overview and the roadmap's Phase 7 status
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Product-Overview.md, Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-11-24
completed:
---
# BL-1651 — Fix stale built-or-not statements in the product overview and the roadmap's Phase 7 status

## Goal

Every "built" or "not built" statement in the product overview and the roadmap's status
lines is true of the code at the commit the task runs on.

## Context

Align-and-document audit, 2026-11-24 (line numbers at commit `bb654e099`):

- `Product-Overview.md:714` - heading "Not built yet, stated as intent" introduces LDAP as
  an account source (BL-671 to BL-673), the Curl port clients (BL-669, BL-670) and store
  test control, all Done; the roadmap's shared-stores track says they are built.
- `Product-Overview.md:754` - "inside LDAP's `GSS-SPNEGO` it is in scope, not built yet
  (BL-327), as is `Proxy-Authenticate` for the proxies": the roadmap's Milestone 5 says
  BL-327 built it, and `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary` handles
  `Proxy-Authenticate`. Verify each in the source before rewording.
- `Product-Overview.md:778` - "In scope, not built yet: `-w` style output per exchange":
  verify no code exists; if none, keep as intent and check a task exists, filing one if not.
- `Roadmap.md:183` - Milestone 7's status says "on `linux-x64` and `osx-arm64` not yet
  closed", while the same entry and its exit criteria say the closing condition holds in
  all four parts (BL-1648).
- `Roadmap.md:143-144` and `:270-271` say "no task filed" for the release tag and package
  publishing; BL-1657 is now that task.

## Acceptance criteria

- [ ] `Product-Overview.md` no longer calls LDAP accounts, the port's shared-store clients,
      store test control, Kerberos in `GSS-SPNEGO` or `Proxy-Authenticate` unbuilt, each
      reworded statement citing the task or type that built it.
- [ ] The `-w` statement is either removed (built) or kept as intent with a task ID.
- [ ] Milestone 7's status line agrees with its own exit criteria.
- [ ] The two "no task filed" phrases name BL-1657.

## Notes

## Log

- 2026-11-24: Created.
