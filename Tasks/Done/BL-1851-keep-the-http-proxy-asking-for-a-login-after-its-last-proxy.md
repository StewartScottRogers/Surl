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
completed: 2026-11-27
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

- [x] A named test composes a proxy with one proxy account, removes it, and gets `Challenge` for a `GET` and a
      `CONNECT` without credentials; the same for an empty `--proxy-user-file` at start.
- [x] A named test shows a proxy composed with no proxy account still proceeds for a `GET` without credentials.
- [x] A named test shows that with `--allow-anonymous` a `GET` without credentials still proceeds after the removal.
- [x] `ComposedControlTarget` writes
      `surl: warning: no proxy account left; without --allow-anonymous the proxy now lets no new client through`,
      once, on the removal that leaves no proxy account, pinned by `ComposedControlTargetAccountRemovalTests`.
- [x] No XML document or `CLAUDE.md` in `Surl.Composition.UnitLibrary` says such a proxy needs no login.

## Notes

- `ComposeProxyPolicy` sets `RequiresLoginWithoutAccounts` on the policy over proxy accounts only. The tests are in
  `ProxyCompositionTests`, served through the composed proxy; `Challenge` is checked as the `407` with a `Digest`
  challenge and no origin connection: `Start_TheLastProxyAccountRemoved_ChallengesARequestWithoutCredentials`,
  `Start_AnEmptyProxyUserFile_ChallengesARequestWithoutCredentials`, `Start_NoProxyAccount_ForwardsAGetWithoutCredentials`
  and `Start_AllowAnonymousAndTheLastProxyAccountRemoved_ForwardsAGetWithoutCredentials`.
- The remaining "needs no login" lines in `Surl.Composition.UnitLibrary` are about the server accounts, which are
  BL-1865's (ADR-0132 amendment 5). `Surl.Api` and `surl-mcp` still write the old proxy warning: BL-1852 and BL-1853.
- Verified: build clean, `dotnet format --verify-no-changes` clean, fast tests green. No Integration run: no network or
  file-system code changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. A proxy composed with proxy accounts now challenges requests without credentials after the last account is removed or from an empty --proxy-user-file, and the control endpoint writes the ADR-0132 amendment 4 warning.
