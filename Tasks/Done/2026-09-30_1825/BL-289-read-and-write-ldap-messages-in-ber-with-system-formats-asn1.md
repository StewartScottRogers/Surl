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
completed: 2026-09-30
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

- [x] Tests in `Surl.Protocol.Ldap.UnitTests` decode every request type and every filter choice in
      Context and encode every response type, round-tripping through `AsnReader` with BER rules, and
      pass.
- [x] Tests show an indefinite length, a message over the given maximum (refused before its value is
      read), a truncated message, a filter nested past the bound and each malformed case reported
      as its own outcome, with the message ID when readable.
- [x] `dotnet build Surl.Protocol.Ldap.UnitLibrary -warnaserror` is clean; the fast tests are green;
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ldap.UnitLibrary`.

## Notes

- Shape: `LdapMessageFrameReader` (framing off `IConnection`, outcomes in `LdapFrameReadOutcome`),
  `LdapMessageDecoder` + `LdapFilterDecoder` over `LdapBerFieldReader` (outcomes in
  `LdapDecodeOutcome`, result `LdapDecodeResult` carrying the message ID once read), and
  `LdapMessageEncoder` for BindResponse, SearchResultEntry/Done/Reference, ExtendedResponse and the
  Notice of Disconnection. 96 tests; 100% line and branch, worst CRAP 10.
- Choices taken (defaults, RFC 4511 alone, no ADR needed):
  - Framing accepts at most four long-form length octets; five or more (legal BER with leading
    zeros, never sent by a real client) is `MalformedLength`, and a length no array can hold is
    `MessageTooLarge` even with no limit. The limit counts tag and length, as MQTT's does.
  - Indefinite length is refused both for the whole message (framing) and for any element inside it
    (decoder, `IndefiniteLength`), since RFC 4511 section 5.1 forbids it everywhere.
  - An authentication choice other than `simple` or `sasl` decodes as
    `LdapUnsupportedAuthentication` rather than malformed, so the server can answer
    `authMethodNotSupported` (7) as RFC 4511 section 4.2 expects.
  - Empty `and`/`or` sets decode (RFC 4526 absolute true/false); an empty `substrings` or an
    `extensibleMatch` with neither rule nor type is `InvalidValue`; a misplaced substring part is
    `UnexpectedTag`. Filter depth counts the top filter as 1 and is checked before descending.
  - Scope is strict 0..2 (no `subordinateSubtree` extension); anything else is
    `EnumerationOutOfRange`.
  - Values and passwords stay `byte[]`; DNs, attribute descriptions and OIDs are strict UTF-8, and
    invalid UTF-8 is `InvalidValue`.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Protocol.Ldap reads LDAPMessages off a connection under the message limit, decodes Bind, Unbind, Search (every filter choice), Abandon, Extended and controls with typed malformed outcomes, and encodes every response in BER
