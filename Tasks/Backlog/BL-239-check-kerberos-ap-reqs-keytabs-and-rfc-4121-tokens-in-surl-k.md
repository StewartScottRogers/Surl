---
id: BL-239
title: Check Kerberos AP-REQs, keytabs and RFC 4121 tokens in Surl.Kerberos
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-238]
touches: [Surl.Kerberos.UnitLibrary, Surl.Kerberos.UnitTests]
requirement: FR-046
created: 2026-09-30
completed:
---
# BL-239 — Check Kerberos AP-REQs, keytabs and RFC 4121 tokens in Surl.Kerberos

## Goal

`Surl.Kerberos` reads an MIT keytab, checks a client's RFC 1964 `InitialContextToken` (Kerberos
AP-REQ) for a service, and gives back a `KerberosSecurityContext` that makes the AP-REP and the
RFC 4121 wrap and MIC tokens, with a replay cache refusing a replayed authenticator.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md`.
  - decision 1: the MIT keytab format: version `0x05 0x02`, then big-endian length-prefixed entries
    of principal (realm, components), timestamp, 8-bit kvno (overridden by the optional trailing
    32-bit kvno when present and not 0), enctype and key; a negative length is a deleted entry,
    skipped; version `0x0501` is malformed. Entries with an enctype outside decision 3 are
    skipped and reported as skipped (the command line turns them into warnings, BL-240).
  - decision 2: a ticket is accepted for `<service>/<host>@<REALM>` when the keytab holds a key for
    that principal; the service and the realm compare case-insensitively (ordinal), the rest
    ordinally; the host and realm are whatever the keytab names. The caller passes the service
    word (`HTTP`, `smtp`, `imap`, `pop`).
  - decision 4: the AP-REQ check, steps 1 to 7, in order: the GSS-API framing (`[APPLICATION 0]`,
    OID `1.2.840.113554.1.2.2`, or `1.2.840.48018.1.2.2` only inside SPNEGO, `TOK_ID` `01 00`,
    `KRB_AP_REQ` with `pvno` 5 and `msg-type` 14); `use-session-key` refused; the key chosen by
    `sname`/`realm`, `enc-part.etype` and `kvno` (the highest for the enctype when `kvno` is
    absent); the ticket decrypted with key usage 2, the `invalid` flag refused, now within
    `[starttime - skew, endtime + skew]` (`authtime` when `starttime` is absent), `caddr`,
    `transited` and `authorization-data` not checked; the authenticator decrypted with the session
    key, key usage 11, `crealm`/`cname` equal to the ticket's (ordinal), `ctime`/`cusec` within the
    skew, `cksum` of type `0x8003`, at least 24 bytes, `Lgth` 16, `GSS_C_MUTUAL_FLAG` (2) read, a
    delegated credential skipped and never stored; the replay cache; the context key is the
    authenticator's `subkey` else the session key, and the client's `seq-number` is kept. Skew is
    300 seconds on the injected `TimeProvider`. DER is read with `System.Formats.Asn1`; malformed
    DER, trailing bytes and a missing required field are refusals, never exceptions. Refusal
    reasons, in the ADR's words: `no key for <principal> <enctype> kvno <n>`, `ticket expired`,
    `clock skew`, `replayed authenticator`, `integrity check failed`, `malformed token`. Integrity
    and checksum comparisons use `CryptographicOperations.FixedTimeEquals`. Key bytes and decrypted
    parts are never in a reason.
  - decision 5: an AP-REP only when `ap-options` has `mutual-required`: `EncAPRepPart` with the
    authenticator's `ctime`/`cusec`, no subkey, a 4-byte `seq-number` from `IKerberosRandomSource`,
    encrypted with the ticket's session key under key usage 12, framed `[APPLICATION 0]`, the
    Kerberos OID, `TOK_ID` `02 00`, the `KRB_AP_REP`. Wrap (`TOK_ID` `05 04`) and MIC (`04 04`)
    tokens per RFC 4121 section 4.2, `SentByAcceptor` set on surl's, key usages 22 (acceptor
    seal), 23 (acceptor sign), 24 (initiator seal), 25 (initiator sign); surl wraps without
    confidentiality and with a right rotation count of 0; tokens read from a client may have either
    confidentiality flag and any rotation count, undone before the check; sequence numbers must be
    the client's next one. RFC 1964's older token formats are not read.
  - decision 6: the public surface (`KerberosKeytab` with `Read`, `KerberosKeytabReadResult`,
    `KerberosKeytabEntry`, `KerberosPrincipalName`, `KerberosAcceptor` with `Accept`,
    `KerberosAcceptResult`, `KerberosSecurityContext`, `KerberosReplayCache`,
    `IKerberosRandomSource`), one public type per file in namespace `Surl.Kerberos`; members may be
    renamed where the code shows a truer name. `KerberosPrincipalName.ToString()` is the RFC 1964
    display form: components joined with `/`, then `@` and the realm, with `/`, `@` and `\` inside a
    name escaped by `\` (decision 10).
  - decision 7: one replay cache per process holding the SHA-256 of each accepted authenticator's
    cipher text with its `ctime`; an entry drops once `ctime + skew` has passed on the
    `TimeProvider`; at most 65536 entries, and when full a new AP-REQ is refused
    (`replay cache full`) rather than evicting one.
  - decision 11: tickets and authenticators are made by hand in the tests as RFC 4120 section 5
    DER, with a fixed service key, a fixed session key and a fixed confounder, encrypted with
    BL-238's vector-checked profiles; keytabs are hand-written byte arrays; time is a fake
    `TimeProvider`, randomness an injected `IKerberosRandomSource`. No KDC, no network.
- Builds on BL-238's internal enctype profiles and `KerberosEncryptionType` in
  `Surl.Kerberos.UnitLibrary`.

## Acceptance criteria

- [ ] `KerberosKeytabTests` read hand-written byte-array keytabs and pin: entries of all four
      enctypes; a deleted (negative-length) entry skipped; a trailing 32-bit kvno overriding the
      8-bit one, and ignored when 0; an `rc4-hmac` (23) entry skipped and reported with its enctype
      and principal; version `0x0501` and a truncated entry each reported malformed at the exact
      byte offset.
- [ ] `KerberosPrincipalNameTests` pin the display forms `user@EXAMPLE.COM` and
      `host/web01@EXAMPLE.COM`, and the `\`-escaping of `/`, `@` and `\` inside a component.
- [ ] `KerberosAcceptorTests` accept a hand-built AP-REQ for each of the four enctypes and return a
      context whose `ClientPrincipal` is the ticket's client; accept the `1.2.840.48018.1.2.2` OID
      only when the token came inside SPNEGO; match the service and realm case-insensitively; and
      refuse, with the reason in ADR-0057 decision 4's words and no exception thrown: a wrong
      service, no key for the principal/enctype/kvno, a tampered ticket, a ticket past
      `endtime + 300 s` on the fake `TimeProvider`, an authenticator outside the 300-second skew,
      `use-session-key`, an `invalid` ticket, an authenticator `cname` differing from the ticket's,
      a checksum not of type `0x8003` or shorter than 24 bytes, a replayed authenticator, malformed
      DER and trailing bytes.
- [ ] `KerberosSecurityContextTests` pin: `IsMutualAuthenticationRequested` follows
      `mutual-required`; `CreateApRepToken` decrypts (in the test) under key usage 12 to the
      authenticator's `ctime`/`cusec` and the injected 4 random bytes as `seq-number`; `Wrap` gives
      `TOK_ID` `05 04` with `SentByAcceptor` set, no confidentiality and RRC 0, checksummed under key
      usage 22 (RFC 4121 section 4.2.4 uses the seal usage for every wrap token);
      `TryUnwrap` reads client tokens with and without confidentiality (key usage 24), with RRC 0
      and non-zero, and returns false for a tampered token, a wrong sequence number and an
      acceptor-flagged token; `GetMic` uses key usage 23 and `VerifyMic` key usage 25.
- [ ] `KerberosReplayCacheTests` pin expiry at `ctime + 300 s` on the fake `TimeProvider`, and that
      with 65536 live entries the next AP-REQ is refused with `replay cache full` and no entry is
      evicted.
- [ ] A test asserts that the refusal reason for a wrong-key case contains neither the key's bytes
      in hex nor any decrypted field.
- [ ] `dotnet build Surl.Kerberos.UnitLibrary -warnaserror` is clean;
      `dotnet test --filter "TestCategory!=Integration"` passes; no test needs
      `TestCategory=Integration` or a KDC; every test is platform-neutral.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch
      coverage, no method over complexity 10 and no CRAP score over 30 for
      `Surl.Kerberos.UnitLibrary`.

## Notes

- No command-line option, no composition and no change to `Surl.Authentication` here; those are
  BL-240 and BL-241. `KerberosReplayCache` is constructed once and shared by every listener, since
  BL-240 composes one per process.

## Log

- 2026-09-30: Created.
- 2026-09-30: Filed by BL-217 (ADR-0057 decision 12).
