---
id: BL-289
title: Read and write LDAP messages in BER with System.Formats.Asn1 in Surl.Protocol.Ldap
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Protocol.Ldap.UnitLibrary, Surl.Protocol.Ldap.UnitTests]
requirement: FR-049
created: 2026-09-30
completed:
---
# BL-289 — Read and write LDAP messages in BER with System.Formats.Asn1 in Surl.Protocol.Ldap

## Goal

`Surl.Protocol.Ldap.UnitLibrary` reads and writes the LDAPv3 messages of RFC 4511 in BER with the
BCL's `System.Formats.Asn1`, as internal types with no transport, so the directory and server tasks
(BL-306, BL-308, BL-309) build on a codec already held to the quality gates.

## Context

- Groundwork decided by RFC 4511 alone; it needs no ADR and runs beside the decision tasks. The BCL
  already reads BER: `AsnReader`/`AsnWriter` with `AsnEncodingRules.BER` are in the shared framework
  (`Surl.Kerberos.UnitLibrary/KerberosDer.cs` and `Surl.Authentication.UnitLibrary/SpnegoToken.cs`
  use them with no package), so no hand-built BER library is needed.
- What to build (RFC 4511 section 4 and appendix B's ASN.1):
  - message framing off an `IConnection`: read one complete `LDAPMessage` TLV - tag, a definite
    length (RFC 4511 section 5.1 allows only definite lengths; refuse the indefinite form) - with the
    length checked against a maximum the caller gives before the value is read (ADR-0006:
    `--max-message` bounds an LDAP message), end of stream and truncation reported;
  - decoding into typed requests: `messageID`; `BindRequest` (version, name, `simple` or `sasl`
    with mechanism and optional credentials); `UnbindRequest`; `SearchRequest` (base, scope,
    `derefAliases`, `sizeLimit`, `timeLimit`, `typesOnly`, the full `Filter` choice - `and`, `or`,
    `not`, `equalityMatch`, `substrings` with `initial`/`any`/`final`, `greaterOrEqual`,
    `lessOrEqual`, `present`, `approxMatch`, `extensibleMatch` - and the attribute list);
    `AbandonRequest`; `ExtendedRequest` (name OID and value); `controls`; any other protocol op
    reported by its tag so the server can answer it; filters nested deeper than a caller-given
    bound refused (a peer-controlled recursion);
  - encoding responses: `BindResponse` (with `serverSaslCreds`), `SearchResultEntry` (DN and
    attributes with values), `SearchResultDone`, `SearchResultReference`, `ExtendedResponse`
    (with `responseName` and `responseValue`), and the Notice of Disconnection (RFC 4511 section
    4.4.1, message ID 0), each `LDAPResult` with result code, matched DN, diagnostic message and
    referral;
  - malformed input (wrong tag, bad length, trailing bytes inside a sequence, an out-of-range
    enumeration) reported as a typed outcome that carries the message ID when it could be read, so
    the server can answer `protocolError` (2).
- Test bytes: build requests with `AsnWriter` in the tests, and include the RFC 4511 examples and
  the encodings of RFC 4515's filter examples (e.g. `(cn=Babs Jensen)`, `(!(cn=Tim Howes))`,
  `(&(objectClass=Person)(|(sn=Jensen)(cn=Babs J*)))`, `(o=univ*of*mich*)`). What pinned upstream
  curl sends is recorded by BL-284 and replayed by BL-308, not here.
- Constraints: internal types, `InternalsVisibleTo` the tests (already in the csproj); complexity at
  most 10 per method; no new package.

## Acceptance criteria

- [ ] Tests in `Surl.Protocol.Ldap.UnitTests` decode every request type and every filter choice in
      Context and encode every response type, round-tripping through `AsnReader` with BER rules, and
      pass.
- [ ] Tests show an indefinite length, a message over the given maximum (refused before its value is
      read), a truncated message, a filter nested past the bound and each malformed case reported
      as its own outcome, with the message ID when readable.
- [ ] `dotnet build Surl.Protocol.Ldap.UnitLibrary -warnaserror` is clean; the fast tests are green;
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ldap.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
