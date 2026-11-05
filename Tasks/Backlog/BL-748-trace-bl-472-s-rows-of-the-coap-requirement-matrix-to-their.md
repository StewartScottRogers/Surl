---
id: BL-748
title: Trace BL-472's rows of the CoAP requirement matrix to their named tests
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-472]
touches: [Documentation/Conformance/Extended/Coap.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-748 — Trace BL-472's rows of the CoAP requirement matrix to their named tests

## Goal

Every row of `Documentation/Conformance/Extended/Coap.md` that names BL-472 names, in its place, the
tests in `Surl.Extended.Protocol.Coap.UnitTests` that prove it, or the task that still owes it.

## Context

- ADR-0097, "Consequences": each task replaces its ID in the matrix with the named test.
- BL-472's tests: `CoapProtocolServerTests` (bytes written by hand from RFC 7252), `CoapFlowSessionTests`
  (separate responses, retransmission, ACK and RST matching, duplicates), `CoapResourceAnswererTests`
  (methods, conditions, response codes, access rules), `CoapRequestOptionsTests`,
  `CoapResourceDirectoryTests`, `CoapDuplicateCacheTests`.
- Rows BL-472 did not prove belong to BL-746 (a proxy request naming the listener, RFC 7252 section
  5.7.2) and BL-747 (decision 7's limits and `5.03`): name those tasks instead.

## Acceptance criteria

- [ ] `Select-String -Path Documentation/Conformance/Extended/Coap.md -Pattern 'BL-472'` finds nothing.
- [ ] Every test name the matrix gives exists in `Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests`.

## Notes

Filed by BL-472: the matrix file was outside its `touches`.

## Log

- 2026-11-05: Created.
