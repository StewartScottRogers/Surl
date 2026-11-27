---
id: BL-1864
title: Rename CoapAccessRules.HasAccounts to IsLoginRequiredToRead and prove a removed last account leaves coap closed
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1863]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-27
completed:
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

- [ ] `CoapAccessRules.HasAccounts` is renamed `IsLoginRequiredToRead`, and `HasAccounts` appears in no file of the
      CoAP library or its tests.
- [ ] A named test in `Surl.Composition.UnitTests` composes the `coap` rules with one account, removes it, and gets
      `MayRead(false)` `false`; after a reset of accounts built empty it gets `true`.
- [ ] The XML documents of `CoapAccessRules` and `MayRead` and the library's `CLAUDE.md` say a read needs a login
      once `coap` has had an account, until a reset or restore returns a state in which it had none.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
