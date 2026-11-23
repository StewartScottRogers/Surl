---
id: BL-1609
title: Write the RFC 1929 password length byte as 4 before **** in the SOCKS5 exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Protocol.Socks.UnitLibrary, Surl.Protocol.Socks.UnitTests]
requirement: none
created: 2026-11-22
completed:
---
# BL-1609 — Write the RFC 1929 password length byte as 4 before **** in the SOCKS5 exchange log

## Goal

The SOCKS5 exchange log (`-v`, `--trace`, `--trace-ascii`, `--log-file` and `Surl.Api`'s `Log`) writes the RFC 1929 `PLEN` byte as `0x04` followed by `****`, whatever the password's real length, so the log never reveals how long the password is.

## Context

This comes from item 3 of the Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T2157-Surl-bug-report.md`. The repro is `surl socks5h://127.0.0.1:1080/ -U app:socks-pw --allow-plaintext-auth -v --log-file open.log` with a client that logs in as `app:socks-pw`. `open.log` then shows `\x07****`, and the 7 is the password's length.

The cause is in `Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary/Socks5PasswordRedactor.cs`. Line 50 writes every byte as it was received before `Advance` runs, so in state `PasswordLength` the real `PLEN` byte is written. `BeginPassword` (lines 121-133) then writes only the `****` mask, and nothing at all for a zero-length password.

How ADR-0182 decides it (`Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md`):
- Decision 1 says: "The secret's length is not shown: `****` is fixed, so a log never reveals it".
- The ADR rejects the alternative "Show the secret's length (`PASS ******`): leaks a hint for no gain".
- Decision 6 says a binary redactor logs header bytes as received. The length prefix of the secret field itself is what decision 1 forbids revealing, so it is not one of those header bytes.

The binary redactors listed here already rewrite the secret's own length to name the mask:
- `Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary/MqttConnectBytesRedactor.cs` ("replaced by a length of 4 and `****`")
- `Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary/Mqtt5ClientSecretBytesRedactor.cs`
- `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary/Amqp0LoginBytesRedactor.cs`

SOCKS5 follows them: `PLEN` becomes `LineBytesRedactor.SecretMask.Length` (4). An empty password (`PLEN` 0) is also written as `0x04 ****`, so the log cannot tell an empty password from any other.

What does not change:
- The redactor still masks byte by byte. The `PLEN` byte is written when it arrives, as `0x04` plus the mask, so a split at any byte gives the same log.
- The event stream keeps raw bytes (ADR-0182 decision 4) and is not touched.
- No byte Surl sends changes, so nothing is measured against upstream curl.

Stand-in: no store, event, fault or option changes. A test that asserts what the client sent still reads `ExchangeBytesEvent`, which keeps the real `PLEN`.

## Acceptance criteria

- [ ] In `Protocols/Proxy/Surl.Protocol.Socks.UnitTests/SocksExchangeLogRedactionTests.cs`, `Masked()` expects `[1, 5, .. "alice"u8, 4, .. "****"u8]`, and `Socks5Subnegotiation_IsLoggedWithPasswdMasked`, `Socks5Subnegotiation_SplitAtEveryByte_IsLoggedTheSame` and `Socks5Subnegotiation_OneByteAtATime_IsLoggedTheSame` pass with it.
- [ ] A new test, `Socks5Subnegotiation_PasswordsOfDifferentLengths_AreLoggedTheSame`, shows that a 1-byte password and a 255-byte password log the same bytes after the user name (`04 2A 2A 2A 2A`).
- [ ] `EmptyUserNameAndEmptyPassword_AreLoggedAsReceived` is replaced by `EmptyPassword_IsLoggedAsFourAndTheMask`, which expects `[1, 0, 4, .. "****"u8]` and passes.
- [ ] `Socks5PasswordRedactor`'s XML summary and the library's `CLAUDE.md` both state that the `PLEN` byte is written as 4 (ADR-0182 decision 1).
- [ ] `dotnet build Protocols/Proxy/Surl.Protocol.Socks.UnitTests -warnaserror` is clean.
- [ ] `dotnet test Protocols/Proxy/Surl.Protocol.Socks.UnitTests --filter "TestCategory!=Integration"` passes, with `Socks5PasswordRedactor` at 100% line and branch coverage.

## Notes

`Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapBindBytesRedactor.cs` keeps "every tag and length octet ... as received", so an LDAP simple bind shows its password's length the same way. That is outside this task's `touches`; it was reported to the planner's caller separately.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
