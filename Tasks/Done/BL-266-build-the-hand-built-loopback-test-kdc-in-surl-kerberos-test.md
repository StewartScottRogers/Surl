---
id: BL-266
title: Build the hand-built loopback test KDC in Surl.Kerberos.TestKdc
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Kerberos.TestKdc.UnitLibrary, Surl.Kerberos.TestKdc.UnitTests, Surl.slnx, Surl.Kerberos.UnitLibrary]
requirement: FR-046
created: 2026-09-30
completed: 2026-09-30
---
# BL-266 — Build the hand-built loopback test KDC in Surl.Kerberos.TestKdc

## Goal

`Surl.Kerberos.TestKdc.UnitLibrary` is a hand-built KDC test fixture for realm `SURL.TEST` that
issues AES tickets through the AS and TGS exchanges over injected UDP and TCP transports and
writes its service principals' keys as an MIT keytab `surl --keytab` reads.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0065-kerberos-logins-are-proved-against-pinned-upstream-curl-through-a-hand-built-loopback-kdc.md` decision 1; the Kerberos pieces it reuses are in `Surl.Kerberos.UnitLibrary`
  (ADR-0057 decisions 3, 6).
- RFC 4120 sections 3.1 (AS), 3.3 (TGS), 5 (ASN.1), 7.2 (UDP and TCP framing); RFC 3962
  (string-to-key, AES-CTS); RFC 4120 section 5.2.7.5 (`PA-ETYPE-INFO2`).
- Test fixture only: not registered in `Surl.Console`, no option, no `--aihelp` topic;
  referenced only by test projects. Base class library only; created with the `new-project` skill.
- It is not a protocol server, so ADR-0002 decision 3's table does not list it; it references
  `Surl.Kerberos.UnitLibrary` and, if it needs one, `Surl.Protocol.Abstractions.UnitLibrary`'s transport seam.

## Acceptance criteria

- [x] `Surl.Kerberos.TestKdc.UnitLibrary` and `.UnitTests` exist, listed in `Surl.slnx` in
      sorted order, and `dotnet build` is clean.
- [x] An AS-REQ without pre-authentication is answered `KDC_ERR_PREAUTH_REQUIRED` carrying
      `PA-ETYPE-INFO2` with the default salt; one with a valid `PA-ENC-TIMESTAMP` gets an AS-REP
      whose TGT a test decrypts with the ticket-granting key.
- [x] A TGS-REQ for a configured service principal gets a TGS-REP whose ticket
      `KerberosAcceptor` accepts from a keytab the fixture wrote; an unknown principal is
      `KDC_ERR_S_PRINCIPAL_UNKNOWN`, an unsupported enctype list `KDC_ERR_ETYPE_NOSUPP`.
- [x] Enctypes 17 and 18 (and 19, 20) are chosen in the client's order; `rc4-hmac` alone is refused.
- [x] UDP and TCP framing (RFC 4120 section 7.2) are both served through injected transports; a
      request over 64 KiB is refused without a crash.
- [x] The library meets the quality gates: 100% line and branch coverage, complexity at most 10,
      CRAP at most 30; fast tests green.

## Notes

- Filed by BL-242 (ADR-0065).
- **Touches widened**: `Surl.Kerberos.UnitLibrary` added. The KDC reuses its enctype profiles,
  `KerberosDer` and message readers, which are internal, so its csproj gains `InternalsVisibleTo`
  for `Surl.Kerberos.TestKdc.UnitLibrary` and `.UnitTests` (and its CLAUDE.md names the new
  consumer). No task in Doing touched it.
- **What was built**: `KerberosTestKdc` (AS and TGS exchanges, `Answer` never throws),
  `KerberosTestKdcServer` (UDP flow, TCP connection, and a loop over both listeners for BL-267),
  `KerberosStringToKey` (RFC 3962 and RFC 8009, pinned by both RFCs' vectors), `MitKeytabWriter`,
  `KerberosDerWriter`, `KerberosKdcMessages`, `KerberosKdcRequest`, public `KerberosErrorCode`.
  108 tests; `Measure-CodeQuality.ps1 -Library Surl.Kerberos.TestKdc.UnitLibrary`: line 100%,
  branch 100%, 0 failing members, worst CRAP 10.
- **Defaults taken** (fixture choices, all RFC-grounded; BL-268's measurement may revisit them):
  - Every principal has keys in all four enctypes; the session key and the ticket's server key
    use the first AES enctype in the client's `etype` list; the AS-REP is encrypted in it too.
  - Error order in an AS-REQ: unknown client (6), unknown server (7), no AES enctype (14), then
    pre-authentication (25, 24, 37). `PA-ETYPE-INFO2` lists the client's AES enctypes in its
    order with the default salt `SURL.TESTtester` and no `s2kparams` (default iteration counts).
  - A one-component `cname` holding `@` (`tester@SURL.TEST`, an RFC 6806 enterprise name, as
    Windows sends for `-u tester@SURL.TEST`) is canonicalised to `tester@SURL.TEST`.
  - Ticket lifetime at most 10 hours (MIT's default); a `till` of `19700101000000Z` means "the
    latest", one not after the start is `KDC_ERR_NEVER_VALID` (11).
  - TGT flags `initial` and `pre-authent`; service tickets `pre-authent`; no PAC; kvno 1.
  - A TGS-REQ's authenticator checksum and `ctime` are not checked (a fixture's simplification).
  - UDP answers longer than 4096 bytes (MIT's KDC default) become `KRB_ERR_RESPONSE_TOO_BIG`;
    requests over 64 KiB, UDP or TCP, are `KRB_ERR_FIELD_TOOLONG` (61), the TCP one refused
    from its length prefix without reading the body. One request per TCP connection.
- **For BL-268 to check against measured Windows bytes**: a KRB-ERROR echoes an enterprise
  `cname` as `NT-PRINCIPAL` (surl's DER reader drops name types); a nonce sent as a negative
  Int32 is read as its two's complement but echoed as a positive integer.
- **Follow-up filed**: BL-270, the Product Overview's project map (BL-214, in Doing, touches
  that file, so this task did not edit it).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Kerberos.TestKdc serves SURL.TEST's AS and TGS exchanges over injected UDP and TCP and writes a keytab KerberosAcceptor accepts
