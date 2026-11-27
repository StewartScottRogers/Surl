---
id: BL-1843
title: Say in the control endpoint's account docs that adding a name already taken answers 409
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1829]
touches: [Application/Surl.Control.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1843 — Say in the control endpoint's account docs that adding a name already taken answers 409

## Goal

The control library's `CLAUDE.md` and XML doc comments for the three account additions give `409` for a name that
already has an account, as well as for a store that is not configured.

## Context

Found while running BL-1829 (checked against the code on 2026-11-27). Prose and XML doc comments only; no
executable line changes. No store, event or fault changes.

- `Application/Surl.Composition.UnitLibrary/ComposedControlTarget.cs`: `AddAccountAsync`, `AddProxyAccountAsync`
  and `AddTlsSrpAccountAsync` refuse a name already taken with `ControlRefusalKind.Conflict` and keep the account
  (ADR-0132 amendment 1; for the account book a second Bearer-token account too, amendment 2). The proxy and
  TLS-SRP ones also answer `Conflict` when that store is not configured.
- `Application/Surl.Control.UnitLibrary/CLAUDE.md`, "Account routes" bullet: gives `409` only "when the store is
  not composed", and says nothing of `POST /v1/accounts` answering `409`.
- `ControlStoreRequests.cs`: the `<returns>` of `AddAccountAsync` gives `204` only; those of
  `AddProxyAccountAsync` and `AddTlsSrpAccountAsync` give `409` only for an unconfigured store.
- `IControlTarget.cs`: `AddAccountAsync` is already right. The `<returns>` of `AddProxyAccountAsync` and
  `AddTlsSrpAccountAsync` name only the unconfigured store (their summaries say a taken name "is refused as for
  accounts").

## Acceptance criteria

- [x] The `CLAUDE.md` says `POST /v1/accounts`, `/v1/proxy-accounts` and `/v1/tls-srp-accounts` answer `409` for a
      name already taken, keeping the account, and the last two also when the store is not configured.
- [x] It keeps `409` on the proxy and TLS-SRP `DELETE` routes for an unconfigured store only.
- [x] The `<returns>` of `ControlStoreRequests.AddAccountAsync`, `AddProxyAccountAsync` and
      `AddTlsSrpAccountAsync` give `409` for a name already taken, the last two also for an unconfigured store.
- [x] The `<returns>` of `IControlTarget.AddProxyAccountAsync` and `AddTlsSrpAccountAsync` name both reasons for
      `ControlRefusalKind.Conflict`.
- [x] `dotnet build Application/Surl.Control.UnitTests -warnaserror` is clean, and the only changed lines in `.cs`
      files are comments.

## Notes

- 2026-11-27: Checked against `ComposedControlTarget`. The `CLAUDE.md` "Account routes" bullet now says "not
  configured" where it said "not composed", as the "Password changes" bullet does. For the account book, a taken
  name includes a second Bearer-token (empty-name) account.
- Verified: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 30198
  passed, 0 failed, 3 skipped. No integration tests run: only comments and prose changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The control library's account docs give 409 for a name already taken on all three account additions, and for an unconfigured proxy or TLS-SRP store
