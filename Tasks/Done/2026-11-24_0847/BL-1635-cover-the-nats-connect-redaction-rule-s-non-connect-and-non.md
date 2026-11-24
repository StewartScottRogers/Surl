---
id: BL-1635
title: Cover the NATS CONNECT redaction rule's non-CONNECT and non-object branches
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1635 — Cover the NATS CONNECT redaction rule's non-CONNECT and non-object branches

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Nats.UnitLibrary` reports 100% line and 100% branch with no
failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-24 (two full fast runs, identical): Nats 99.59% line, 98.78% branch,
  2 failing members, both from BL-1458 (commit 1372b7059). This is the lowest branch figure in the solution.
- `NatsConnectLineRedactionRule.RedactObject(ReadOnlySpan<byte>, ArrayBufferWriter<byte>)`,
  `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/NatsConnectLineRedactionRule.cs:95`, 90% line, 70%
  branch, Cx 6, CRAP 6.04. This is the worst CRAP of all the findings.
  - Lines 99-100 uncovered: the `JsonException` for a CONNECT argument that is not a JSON object (line 98 is 2 of 4, so
    neither an empty argument (`!reader.Read()`) nor a non-object first token (`[]`, `"x"`, `1`) is tried).
  - Line 104 is 3 of 4: the property loop never ends because `reader.Read()` returns false, as it does for an object cut
    off mid-way, as opposed to ending on `EndObject`.
- `NatsConnectLineRedactionRule.IsOperationIgnoringCase(ReadOnlySpan<byte>)`, same file line 72, 77.78% line, 75%
  branch. Lines 77-78 uncovered: no test gives a word of CONNECT's length that is not `CONNECT` in any case (for example
  `CONNECX` or `CONNECT` with one letter changed).
- The bytes come from the NATS client protocol specification (ADR-0083).

## Acceptance criteria

- [x] A named test redacts a 7-letter operation line that is not CONNECT and asserts it is logged unchanged.
- [x] Named tests redact `CONNECT` with an empty argument and with a non-object JSON argument, and assert what the rule
      logs for each. This is whatever its caller does with the `JsonException`; the test must show no `pass` or
      `auth_token` value in clear.
- [x] A named test redacts a `CONNECT {"user":"a","pass":"secret"` line cut off before `}` and asserts the secret is not
      logged in clear.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Nats.UnitLibrary` reports
      `100 | 100` with 0 failing members, and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Tests only (test-writer). A secret that shows in clear is a defect: file it as its own task, do not fix it here.
- Two branches were unreachable, not untested: with a final-block Utf8JsonReader, Read never returns false for a non-blank argument (truncated input throws JsonException, which the Redact caller masks whole). Added the library to touches and replaced the dead `!reader.Read()` and loop `reader.Read() &&` checks with ignored Reads (RedactObject, ReadsPropertyName); behaviour unchanged. Empty argument cannot reach RedactObject (ArgumentStart returns -1, line logged unchanged).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. Nats redaction rule branches covered; Measure-CodeQuality reports 100|100, 0 failing
