---
id: BL-1432
title: Withhold the LDAP simple bind password from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1419]
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1432 — Withhold the LDAP simple bind password from the exchange log

## Goal

`surl -v --user alice:s3cret --self-signed ldaps://127.0.0.1:0/`, answering
`curl -k -u "uid=alice,dc=example:s3cret" "ldaps://127.0.0.1:<port>/dc=example"`, never logs the
simple bind password's bytes; they are written as BL-1417's ADR says.

## Context

- Bug report item: the parent named LDAP simple bind. A BindRequest (RFC 4511 section 4.2) carries the
  simple password as a context-specific `[0]` OCTET STRING, in clear inside the BER bytes the log
  dumps.
- `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapProtocolServer.cs` is the server;
  `LdapBindJudge.cs:70-77` reads `simple.Password`. The redactor parses BER message framing (`SEQUENCE`
  length, messageID, `[APPLICATION 0]`) across chunk boundaries and rewrites only the `[0]` value,
  keeping lengths as the ADR says. SASL binds (`[3]`) are judged as the ADR's table says (PLAIN in
  clear, GSS-SPNEGO not).
- Messages other than BindRequest pass through untouched; StartTLS (`ldap://` extended request) leaves
  later bytes plaintext to the log.

## Acceptance criteria

- [x] `LdapProtocolServer` implements BL-1418's contract. Fast tests in
      `Protocols/Messaging/Surl.Protocol.Ldap.UnitTests` show a simple BindRequest logged with the
      password in the ADR's form, also when the message is split across reads at every byte.
- [x] A SASL PLAIN bind's credentials are redacted (named test); a SearchRequest whose filter contains
      the password's bytes is logged unchanged.
- [x] A BER length in long form (`0x81 0x..`) is handled (named test).
- [x] `dotnet build Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary -warnaserror` is clean, the fast
      tests are green, and coverage stays at 100% line and branch.

## Notes

- Decided: `LdapBindBytesRedactor` keeps every tag and length octet as received and replaces only the secret value bytes with `****` (ADR-0182 decision 6), so a masked bind is not valid BER. An empty password or SASL credential is left as is (nothing to withhold). SASL `PLAIN` (any case) is masked; other mechanisms and Sicily tokens are logged (ADR-0182 decision 8). A bind over 64 KiB is logged as one `****`; a bind cut off at exchange end is masked from the field it stopped in.
- Bytes after a non-SEQUENCE byte (security-layer buffers, TLS after StartTLS) pass through unchanged.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T0553-Surl-bug-report.md` item 1.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. LdapBindBytesRedactor masks simple bind passwords and SASL PLAIN credentials in the exchange log
