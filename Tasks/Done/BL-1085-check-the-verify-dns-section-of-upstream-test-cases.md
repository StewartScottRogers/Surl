---
id: BL-1085
title: Check the verify dns section of upstream test cases
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-570]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1085 — Check the verify dns section of upstream test cases

## Goal

`UpstreamTestCaseVerifyChecker` checks a case's `<verify><dns>` against the DNS server half's query log, as runtests does.

## Context

- ADR-0123 decision 1: runtests compares `dnsd.input`, both sides sorted and the log filtered by the `host` attribute's pattern, with `<verify><dns>` (`tests/runtests.pl` lines 1743-1762 at tag `curl-8_21_0`). Case 2104 uses it (`host="examplehost.invalid QTYPE A"`).
- BL-570 built `DnsQueryUpstreamTestCaseReplies.QueryLog` (`QNAME <name> QTYPE <type>` lines) but the checker does not read it; no pin lists `override-dns`, so no case reaches the check today.

## Acceptance criteria

- [x] `UpstreamTestCaseRunRecord` carries the DNS query log and `UpstreamTestCaseVerifyChecker.Check` compares it with `<verify><dns>` sorted and filtered by the `host` attribute, with named passing and failing tests.
- [x] The run feeds `QueryLog` into the record.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library keeps 100% line and branch coverage.

## Notes

- DnsQueryLog is an init property on the record (default empty), so existing constructor calls are unchanged. The check sits after proxy and before file; the host attribute is applied as a .NET regex to the log lines, both sides sorted ordinally. No pinned build lists override-dns, so no case exercises it live.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. verify dns section checked against the DNS query log, host-filtered and sorted
