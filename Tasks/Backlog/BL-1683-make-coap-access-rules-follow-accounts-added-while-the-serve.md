---
id: BL-1683
title: Make CoAP access rules follow accounts added while the server runs
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-25
completed:
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

- [ ] A named test in `Surl.Composition.UnitTests` composes the rules with no account, adds one to the book, and
      shows `MayRead(false)` is then false; removing it makes it true again.
- [ ] A named test in `Surl.Extended.Protocol.Coap.UnitTests` shows one running server answers a `GET` `2.05`
      before the account exists and `4.01` after.
- [ ] `dotnet build` is clean, the fast tests are green, and every changed member keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
