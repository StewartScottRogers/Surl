---
id: BL-1274
title: Add AccountBook.TryFindPreSharedKey
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1244]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: FR-061
created: 2026-11-16
completed: 2026-11-16
---
# BL-1274 — Add AccountBook.TryFindPreSharedKey

## Goal

`AccountBook.TryFindPreSharedKey(userName, scheme)` returns the live password bytes of an account admitted to the scheme, so the `coaps` key lookup can read the book per handshake.

## Context

ADR-0170 decision 5. Today `DeferredCoapsProtocolServer` copies the accounts once; the book keeps accounts, so a changed book must change who may log in over `coaps`.

## Acceptance criteria

- [x] `TryFindPreSharedKey` returns the password bytes (UTF-8) for an existing account whose schemes admit the scheme, and none for an unknown user or an account not admitted.
- [x] After `Add`, `Remove`, `Reset` and `Restore` the answer follows the book.
- [x] Surl.Authentication keeps 100% line and branch coverage.

## Notes

- Signature: `bool TryFindPreSharedKey(string userName, string scheme, out byte[] key)`; the key is a copy of the UTF-8 password bytes (read from the account's derived Signature V4 secret, which is that), empty when not found. The empty (Bearer) name never matches. Works through scheme views.
- Tests: `AccountBookTests.PreSharedKey.cs` (8 tests, every branch of the method); the library's 1448 tests pass.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. AccountBook.TryFindPreSharedKey returns live UTF-8 password bytes of accounts admitted to a scheme
