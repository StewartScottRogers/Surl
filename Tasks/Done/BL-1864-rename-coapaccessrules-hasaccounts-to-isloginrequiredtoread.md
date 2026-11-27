---
id: BL-1864
title: Rename CoapAccessRules.HasAccounts to IsLoginRequiredToRead and prove a removed last account leaves coap closed
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1863]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Application/Surl.Composition.UnitTests, Documentation/Conformance/Extended/Coap.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1864 — Rename CoapAccessRules.HasAccounts to IsLoginRequiredToRead and prove a removed last account leaves coap closed

## Goal

The CoAP access rules' question is named for what it answers since ADR-0132 amendment 5: whether a read needs a
login, not whether an account exists.

## Context

- ADR-0132 amendment 5, decisions 3 and 9; it supersedes the removal rule of ADR-0097 amendment 3.
- After BL-1863 the question `Surl.Composition` hands `CoapAccessRules` stays `true` once `coap` has had an account,
  so the name `HasAccounts` and its documents are false.
- Callers: `CoapAccessRulesTests` and the other CoAP tests naming `HasAccounts:`, and `CoapsCompositionTests`.

## Acceptance criteria

- [x] `CoapAccessRules.HasAccounts` is renamed `IsLoginRequiredToRead`, and `HasAccounts` appears in no file of the
      CoAP library or its tests.
- [x] A named test in `Surl.Composition.UnitTests` composes the `coap` rules with one account, removes it, and gets
      `MayRead(false)` `false`; after a reset of accounts built empty it gets `true`.
- [x] The XML documents of `CoapAccessRules` and `MayRead` and the library's `CLAUDE.md` say a read needs a login
      once `coap` has had an account, until a reset or restore returns a state in which it had none.

## Notes

- A rename and its documents only: no behaviour changed, so no new ADR and no byte to measure.
- Named test:
  `CoapsCompositionTests.CoapAccessRulesFor_OnlyAccountRemoved_RefusesAnUnauthenticatedReadUntilAResetOfAccountsBuiltEmpty`,
  two rows: accounts built empty (a reset opens reads) and accounts built with the account (a reset keeps them closed).
- `Surl.Composition.UnitLibrary` needed no change: it passes the question to `CoapAccessRules` by position.
- CoAP test names and locals that said "accounts" where they meant the question were renamed too
  (`Constructor_NullIsLoginRequiredToRead_Throws`, `MayRead_LoginBecomesRequiredAfterConstruction_AsksAgain`).
- Added `Documentation/Conformance/Extended/Coap.md` to `touches`: its row `Coap:ADR0096-6-1` names the renamed test
  `CoapAccessRulesFor_Policy_IsAnonymousSwitchAndWhetherAReadNeedsALogin`. The same row named a `LookUpKey` test that
  does not exist; it now names `LookUpKey_Identity_IsTheLiveBooksPasswordOrNothing`. No task in Doing names that file.
- Gates: build with 0 warnings, format clean, fast tests 30269 passed, 0 failed, 3 skipped. No integration run:
  nothing here touches the network or the file system.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. CoapAccessRules asks IsLoginRequiredToRead, and a test shows removing the only account leaves coap reads closed until a reset of accounts built empty
