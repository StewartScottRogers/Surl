---
id: BL-1048
title: Add CoAP unit tests for the 17 Coap.md rows no test proves
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests, Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Documentation/Conformance/Extended/Coap.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-1048 — Add CoAP unit tests for the 17 Coap.md rows no test proves

## Goal

Every `Documentation/Conformance/Extended/Coap.md` row whose "Covered by" cell says `BL-1048` names a unit test that proves it.

## Context

Found by BL-477 while tracing the matrix. The rows (IDs after `Coap:`): RFC7252-4.7-1, 5.4.4-1, 5.4.5-1, 5.5-1,
5.7.2-1, 5.9.3.4-1, 5.10.2-2, 5.10.8-2, 6.5-1, 9.1.1-3; RFC7959-2.1-2, 2.3-1, 2.3-2, 2.6-3, 2.9.1-1, 2.10-1;
RFC7641-4.2-4. Tests go in `Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests` (and `Surl.Dtls` tests for
9.1.1-3). If a row shows a server defect (5.4.4-1: Max-Age 60 is sent explicitly although it is the default),
fix the server or amend ADR-0097 and the row, deciding under Stewart's delegation.

## Acceptance criteria

- [ ] No row of Coap.md names `BL-1048`; each names a test that exists or is `n/a` with a reason.
- [ ] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

## Log

- 2026-11-11: Created.
