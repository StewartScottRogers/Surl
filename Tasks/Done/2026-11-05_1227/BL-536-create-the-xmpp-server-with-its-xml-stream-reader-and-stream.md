---
id: BL-536
title: Create the XMPP server with its XML stream reader and stream errors
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-535, BL-404]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-536 — Create the XMPP server with its XML stream reader and stream errors

## Goal

`Surl.Extended.Protocol.Xmpp.UnitLibrary` exists. It reads an XML stream incrementally from a connection, one
top-level element at a time, refusing what RFC 6120 section 11 forbids. It opens and closes streams with the
right headers, and ends a broken stream with the right stream error.

## Context

- Decision: BL-535's ADR, decisions 1, 6 and 7. Rules: BL-543's ADR.
- New projects in `Protocols/Messaging/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.Xmpp`.
  References: `Surl.Protocol.Abstractions` only.
- RFC 6120:
  - section 4.2 to 4.3 (opening a stream: `from`, `to`, `id`, `version="1.0"`, `xml:lang`, and the server's
    response header);
  - section 4.4 (closing);
  - section 4.9 (stream errors: `not-well-formed`, `restricted-xml`, `unsupported-version`, `host-unknown`,
    `invalid-namespace`, `policy-violation`, `connection-timeout`);
  - section 11 (XML restrictions).
- The BCL's `XmlReader` with `DtdProcessing.Prohibit`, `Async = true` and fragment conformance, fed from the
  connection. Stream IDs from an injected random source.

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - RFC 6120 section 9.1's opening stream header answered with a header in the shape the RFC shows;
      - an element split across reads delivered whole;
      - two elements in one read delivered in order;
      - a DTD, comment or processing instruction ending the stream with `restricted-xml`;
      - malformed XML ending it with `not-well-formed`;
      - `version="2.0"` getting `unsupported-version`;
      - a wrong `to` getting `host-unknown`;
      - an element over the size limit getting `policy-violation`;
      - the client's `</stream:stream>` answered with the server's.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- **Delivered.** `Surl.Extended.Protocol.Xmpp.UnitLibrary` (`XmppServer`, `XmppServerConnection`,
  `XmppStreamReader` over `XmppInputStream`, `XmppParseFailures`, `XmppDomains`, `XmppStreamText`,
  `XmppStreamErrors`, `XmppNamespaces`, `IXmppRandomSource` with `CryptographicXmppRandomSource`,
  `XmppServerOptions`) and its `.UnitTests`, 143 tests; 100% line and branch coverage, no method over
  complexity 10 (measured from the Cobertura report).
- **Acceptance tests.** RFC 9.1 header: `XmppServerTests.ServeAsync_Rfc6120OpeningHeader_IsAnsweredWithTheRfcsResponseHeaderShape`;
  split element: `XmppStreamReaderTests.ReadElementAsync_ElementSplitAcrossReads_IsDeliveredWhole`; two in one read:
  `ReadElementAsync_TwoElementsInOneRead_AreDeliveredInOrder`; DTD, comment, PI: `XmppServerTests.ServeAsync_RefusedXml_EndsTheStreamWithItsError`
  (and the reader's `..._RefusedHeader_...`, `..._RefusedContent_...`); malformed: the same plus
  `ServeAsync_ElementBeforeAuthentication_EndsTheStreamWithItsError`; `2.0`: `ServeAsync_VersionOtherThanMajor1_IsUnsupportedVersion`;
  wrong `to`: `ServeAsync_WrongTo_IsHostUnknown`; size: `ServeAsync_ElementOverTheSizeLimit_IsPolicyViolation`;
  close: `ServeAsync_ClientClosesTheStream_IsAnsweredWithTheServersClose`.
- **Version rule (decided by Claude under Stewart's delegation).** A major version of 1 opens a `1.0` stream;
  a missing, malformed or other major version (`2.0` included) is `unsupported-version`. This follows RFC 6120
  4.7.5 and 4.9.3.25 (whose example refuses `11.0`) and this task's criterion; ADR-0114 decision 1 said
  "a higher major or minor" opens a stream. The ADR could not be edited here (BL-822 holds
  `Documentation/Planning/Decisions`), so BL-840 amends it.
- **Matrix.** `Documentation/Conformance/Extended/Xmpp.md` still names `BL-536` on its rows: BL-396 holds
  `Documentation/Conformance` in Doing. BL-840 replaces them with test names (as BL-531 left Stomp.md).
- **Choices with a default taken.**
  - Until BL-537 to BL-539, the response header is followed by an empty `<stream:features/>`, and the first
    first-level element ends the stream: a stanza `not-authorized`, another `jabber:client` element
    `unsupported-stanza-type`, any other element (`<starttls/>` included) `policy-violation`.
  - Text (not whitespace) between first-level elements is `policy-violation`, since ADR-0114's table names no
    condition for it and `bad-format` is never sent; text before the header is `not-well-formed`.
  - `XmlReader` reports a DTD, an undeclared entity and an unswitchable encoding as the same `XmlException`,
    so `XmppParseFailures` names the failure from the last 16 KiB read (`<!DOCTYPE`, `&name;` other than the
    five predefined, an XML declaration naming another encoding). Plain string scans, not `GeneratedRegex`,
    whose generated code would count against the coverage gate.
  - Bytes that are not UTF-8 are caught by a strict UTF-8 decoder in `XmppInputStream` (`unsupported-encoding`).
  - The size bound counts bytes from when a first-level start tag is reported, with reads capped at 4096 bytes,
    so an element is measured to within one read of its size and never over it; values (attributes and text)
    are also checked one by one.
  - `ExchangeContext` cannot tell `--idle-timeout` from `--max-time`, so both are answered `connection-timeout`
    (ADR-0114 decision 7 says `--max-time` closes with no bytes); shutdown is `system-shutdown`. Each farewell is
    written within one second on the `TimeProvider`, as `Surl.Protocol.Ws` does. BL-840 records it in the ADR.
  - The login timeout (twice `--head-timeout`) needs SASL and binding, so it stays with BL-538 as the matrix says.
  - The refusal writer has no listen URL, so its header's `from` is the local address as an IP literal, left out
    on a Unix socket. A stream error before any domain is served (a wildcard listen on a Unix socket, no
    `--mail-domain`) is `host-unknown` with no `from`.
  - Only `xmpp` is registered as a scheme; `xmpps` comes with BL-537.
- **Found and fixed while testing.** A text node's value is parsed lazily, so an undeclared entity surfaced from
  `GetValueAsync` outside the classified catch; both reads now go through one handler. Whitespace keepalives under
  `xml:space='preserve'` arrive as `SignificantWhitespace` and are skipped like any other.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The XMPP server reads streams incrementally with XmlReader, opens and closes them with RFC 6120 headers, and ends broken ones with the right stream error
