---
id: BL-1685
title: Offer coaps pre-shared keys to accounts added after the first flow
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1685 — Offer coaps pre-shared keys to accounts added after the first flow

## Goal

An account added while a `coaps` server runs can log in with its pre-shared key, even when no account was admitted
to `coaps` at the server's first flow.

## Context

- `DeferredCoapsProtocolServer.Build` runs once, at the first flow, and passes the pre-shared-key lookup only when
  `book.ForScheme("coaps").HasAccounts` is true then. With a certificate and no account at that moment, the PSK suites
  stay off for the server's life, so an account added later makes reads `4.01` (BL-1683) but cannot log in by PSK.
  ADR-0170 decisions 1 and 5 say an added account decides who may log in over `coaps`.
- With no certificate, and the last account removed before the first flow, `Build` throws `ArgumentException`, which
  the `Lazy` caches: every later flow fails even after an account is added.
- With no account but a lookup present, `Surl.Dtls` treats the PSK suites as live and prefers the certificate suites
  (`DtlsCipherSuite.AllSuitesInServerOrder`). Decide what a PSK-only client gets while no account exists, and record it
  as an ADR-0170 amendment (add that file to `touches`).
- Found while delivering BL-1683.

## Acceptance criteria

- [ ] A named test in `Surl.Composition.UnitTests` serves a first `coaps` flow with a certificate and no account, adds
      an account, and shows a later pre-shared-key handshake with it completes.
- [ ] A named test shows a flow that arrives with no certificate and no account does not stop a flow after an account
      is added from being served.
- [ ] `dotnet build` is clean, the fast tests are green, and every changed member keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created.
