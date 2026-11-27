---
id: BL-1851
title: Keep the HTTP proxy asking for a login after its last proxy account is removed and correct the control endpoint warning
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-1850]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1851 — Keep the HTTP proxy asking for a login after its last proxy account is removed and correct the control endpoint warning

## Goal

A proxy composed with proxy accounts challenges every request without credentials after the last account is removed,
and the control endpoint's removal writes the warning ADR-0132 amendment 4 gives.

## Context

- ADR-0132 amendment 4, decisions 1, 2, 6 and 7.
- `AuthenticationComposition.ComposeProxyPolicy` sets `AuthenticationSettings.RequiresLoginWithoutAccounts` (BL-1850)
  on the policy it builds over proxy accounts, never on `NoAccountProxyAuthenticationPolicy`.
- `ComposedControlTarget.RemoveProxyAccountAsync` writes the old line, pinned by
  `ComposedControlTargetAccountRemovalTests`.

## Acceptance criteria

- [ ] A named test composes a proxy with one proxy account, removes it, and gets `Challenge` for a `GET` and a
      `CONNECT` without credentials; the same for an empty `--proxy-user-file` at start.
- [ ] A named test shows a proxy composed with no proxy account still proceeds for a `GET` without credentials.
- [ ] A named test shows that with `--allow-anonymous` a `GET` without credentials still proceeds after the removal.
- [ ] `ComposedControlTarget` writes
      `surl: warning: no proxy account left; without --allow-anonymous the proxy now lets no new client through`,
      once, on the removal that leaves no proxy account, pinned by `ComposedControlTargetAccountRemovalTests`.
- [ ] No XML document or `CLAUDE.md` in `Surl.Composition.UnitLibrary` says such a proxy needs no login.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
