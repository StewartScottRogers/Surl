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
completed:
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

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - RFC 6120 section 9.1's opening stream header answered with a header in the shape the RFC shows;
      - an element split across reads delivered whole;
      - two elements in one read delivered in order;
      - a DTD, comment or processing instruction ending the stream with `restricted-xml`;
      - malformed XML ending it with `not-well-formed`;
      - `version="2.0"` getting `unsupported-version`;
      - a wrong `to` getting `host-unknown`;
      - an element over the size limit getting `policy-violation`;
      - the client's `</stream:stream>` answered with the server's.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
