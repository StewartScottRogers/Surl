---
id: BL-1683
title: Make CoAP access rules follow accounts added while the server runs
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Documentation/Planning/Decisions/ADR-0097-how-the-coap-server-answers-coap-and-coaps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1683 — Make CoAP access rules follow accounts added while the server runs

## Goal

A `coap` or `coaps` server that starts with no account answers an unauthenticated read with `4.01` once an account
is added while it runs, as the HTTP server does.

## Context

- Security. `CoapAccessRules.HasAccounts` is a `bool` read once: for `coap` when the server is composed
  (`ExtendedProtocolServers.CoapAccessRulesFor`), for `coaps` at its first flow (`DeferredCoapsProtocolServer`).
  Accounts change at run time (ADR-0132, `Surl.Api`, the control endpoint), and `HttpAuthenticationSession` asks per
  request, so an added account leaves CoAP reads open. Removing the last account leaves CoAP private (fails closed).
- Found by the BL-1669 review. ADR-0097 decision 6, ADR-0132, ADR-0149 amendment 2.

## Acceptance criteria

- [x] A named test in `Surl.Composition.UnitTests` composes the rules with no account, adds one to the book, and
      shows `MayRead(false)` is then false; removing it makes it true again.
- [x] A named test in `Surl.Extended.Protocol.Coap.UnitTests` shows one running server answers a `GET` `2.05`
      before the account exists and `4.01` after.
- [x] `dotnet build` is clean, the fast tests are green, and every changed member keeps 100% line and branch
      coverage.

## Notes

- `CoapAccessRules.HasAccounts` is now a `Func<bool>`, asked at each unauthenticated, non-anonymous read.
  `CoapAccessRulesFor` hands it `HasAccountToLogInTo("coap")` on the live `coap` view of the accounts.
- Removing the last account opens reads again, as ADR-0132 does for the other servers; the Context's "stays private"
  was the old behaviour and the first criterion asks for the change. Recorded as ADR-0097 amendment 3, added to
  `touches` (no other task in `Doing` names it).
- Tests: `CoapsCompositionTests.CoapAccessRulesFor_AccountAddedAndRemovedAfterComposing_FollowsTheBook`,
  `CoapProtocolServerTests.ServeAsync_AccountAddedWhileTheServerRuns_GetIs205BeforeAnd401After`,
  `CoapFlowObserversTests.ServeAsync_AccountAddedWhileObserved_Notifies401WithoutObserveAndRemovesTheObserver`.
- Gates: build 0 warnings; fast tests green; `Surl.Extended.Protocol.Coap.UnitLibrary` and `Surl.Composition.UnitLibrary`
  100% line and branch, no failing member. `code-reviewer`: no must-fix; its four optional items are done. No
  conformance stage: upstream curl has no CoAP.
- One early coverage run (whole-solution `dotnet test` under the measure script, PowerShell 7) reported a failed test
  that its filtered output did not name; the later full runs passed.
- `dotnet format --verify-no-changes` reports line-ending and import-order findings in files this task did not change.
- Filed BL-1688: the `coaps` pre-shared-key lookup is still decided once, at the first flow.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. CoAP asks the accounts at each read, so an account added while coap or coaps runs makes unauthenticated reads 4.01
