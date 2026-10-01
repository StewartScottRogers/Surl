---
id: BL-326
title: Give DIGEST-MD5 its LDAP challenge, final rspauth and auth-int and auth-conf layers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-328]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: FR-049
created: 2026-09-30
completed: 2026-09-30
---
# BL-326 — Give DIGEST-MD5 its LDAP challenge, final rspauth and auth-int and auth-conf layers

## Goal

A `DIGEST-MD5` exchange started with `CanCarrySecurityLayer` sends ADR-0072's LDAP challenge,
returns `rspauth` as the accepting step's final data, and, for `qop=auth-int` or `auth-conf`,
an `ISaslSecurityLayer` built as RFC 2831 sections 2.3 and 2.4 say, with the `3des` and `rc4`
ciphers.

## Context

- Decision: ADR-0072 decision 4 (the challenge
  `realm="surl",nonce="...",qop="auth,auth-int,auth-conf",cipher="3des,rc4",maxbuf=65536,charset=utf-8,algorithm=md5-sess`;
  `rspauth` on the final `success`; no unchecked `DIGEST-MD5` under `--allow-anonymous`). ADR-0049
  decision 5 (the mail exchange, unchanged when `CanCarrySecurityLayer` is false).
- Measured (ADR-0072): `WinLDAP` refuses a `qop="auth"`-only challenge, and from the full list
  answers `qop=auth-conf,cipher=3des`, `realm=""`, `digest-uri="ldap/127.0.0.1"`.
- RFC 2831: `Kic`/`Kis`/`Kcc`/`Kcs`, the 10-byte HMAC-MD5 MAC, message type 1, 4-byte sequence
  number, 3DES-CBC (the BCL's `TripleDES`, two-key, IV from the key) and RC4
  (`Surl.Cryptography.Rc4.UnitLibrary`). The final success carries `rspauth` (RFC 4422 section 5);
  how the step carries final data is this task's to add to `SaslLoginStep` if BL-328 did not
  (that would touch Abstractions: re-check `touches` first).
- Fixtures: re-record `curl --digest -u alice:secret` with `Record-CurlExchange.ps1 -Ldap` and a
  fixed nonce; computing `rspauth` needs the recorder to know the password, so record up to the
  response and test the rest from RFC 2831's own layout.

## Acceptance criteria

- [x] A test checks the recorded `WinLDAP` response for `alice:secret` against the recorded nonce and
      computes `rspauth` for it.
- [x] Tests show `auth-conf` with `3des` and `rc4`, and `auth-int`, round-tripping both directions,
      a bad MAC and a wrong sequence number refused, and `auth` giving no layer.
- [x] Tests show the mail challenge unchanged when `CanCarrySecurityLayer` is false.
- [x] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for `Surl.Authentication.UnitLibrary`.

## Notes

- Filed by BL-284 (ADR-0072). BL-309 depends on it.
- Touches widened (2026-09-30): BL-328 left no way for an accepting step to carry final data, so
  `SaslLoginStep` gains `ReadOnlyMemory<byte> AdditionalSuccessData = default` (RFC 4422 section
  5's additional data with success) in `Surl.Protocol.Abstractions.UnitLibrary`, with a contract
  test in its `.UnitTests`. No task in Doing named either project. A new optional member, not
  `Challenge` reused on `Accepted`, so each field says what it holds and no mail server changes.
  ADR-0072 already left the shape to this task; ADRs were not edited because BL-286 (Doing)
  touches `Documentation/Planning/Decisions`.
- Measured: `Fixtures/ldap-digest-md5` (see its README). `WinLDAP` opens the bind with empty
  credentials (present, zero length), so in LDAP mode an empty initial response is answered with
  the challenge, as none is; the mail exchange still refuses one. It then chose
  `qop=auth-conf,cipher=3des`. The recorder cannot compute `rspauth` (no password), and extending
  it was out: `Record-CurlExchange.ps1` is in BL-286's touches. So the sealed buffers are tested
  against RFC 2831's layout computed independently in the test (`H(A1)` and `rspauth` pinned from
  a PowerShell MD5 computation), not against a `WinLDAP` buffer; BL-309's conformance run is the
  end-to-end proof.
- Defaults taken: sequence numbers and the MAC's message type are big-endian (RFC 2831's network
  byte order); the 3DES IV chains across messages from the last ciphertext block (as Cyrus SASL
  does); padding is checked byte for byte; the client's own `maxbuf` is not read (the server's
  buffers are bounded by the LDAP server's limits). The BCL's one-shot `TripleDES.EncryptCbc`
  refuses a 16-byte key on Windows (CNG `0xc100000d`), so the two-key key is written as 24 bytes,
  K1 K2 K1, which is the same cipher.
- Under `--allow-anonymous` LDAP `DIGEST-MD5` is always checked: a user with no account is refused
  with `SecurityLayerNeedsPasswordNote`, as NTLM is (ADR-0072 decision 4).

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. LDAP DIGEST-MD5 sends ADR-0072's challenge, rspauth as AdditionalSuccessData, and auth-int/auth-conf (3des, rc4) security layers
