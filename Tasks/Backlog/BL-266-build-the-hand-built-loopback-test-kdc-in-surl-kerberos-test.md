---
id: BL-266
title: Build the hand-built loopback test KDC in Surl.Kerberos.TestKdc
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Kerberos.TestKdc.UnitLibrary, Surl.Kerberos.TestKdc.UnitTests, Surl.slnx]
requirement: FR-046
created: 2026-09-30
completed:
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

- [ ] `Surl.Kerberos.TestKdc.UnitLibrary` and `.UnitTests` exist, listed in `Surl.slnx` in
      sorted order, and `dotnet build` is clean.
- [ ] An AS-REQ without pre-authentication is answered `KDC_ERR_PREAUTH_REQUIRED` carrying
      `PA-ETYPE-INFO2` with the default salt; one with a valid `PA-ENC-TIMESTAMP` gets an AS-REP
      whose TGT a test decrypts with the ticket-granting key.
- [ ] A TGS-REQ for a configured service principal gets a TGS-REP whose ticket
      `KerberosAcceptor` accepts from a keytab the fixture wrote; an unknown principal is
      `KDC_ERR_S_PRINCIPAL_UNKNOWN`, an unsupported enctype list `KDC_ERR_ETYPE_NOSUPP`.
- [ ] Enctypes 17 and 18 (and 19, 20) are chosen in the client's order; `rc4-hmac` alone is refused.
- [ ] UDP and TCP framing (RFC 4120 section 7.2) are both served through injected transports; a
      request over 64 KiB is refused without a crash.
- [ ] The library meets the quality gates: 100% line and branch coverage, complexity at most 10,
      CRAP at most 30; fast tests green.

## Notes

- Filed by BL-242 (ADR-0065).

## Log

- 2026-09-30: Created.
