---
id: BL-825
title: Send netascii TFTP reads with CRLF line endings
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Tftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Tftp.UnitTests, Documentation/Planning/Decisions, Documentation/Conformance]
requirement: none
created: 2026-11-05
completed:
---
# BL-825 — Send netascii TFTP reads with CRLF line endings

## Goal

A TFTP read in `netascii` mode is sent as RFC 1350 netascii (each LF as CR LF, each CR as CR NUL),
so upstream test 1094 passes.

## Context

- Found in BL-396's upstream test-case run (Windows, reference build, 2026-11-05): test 1094
  (`"tftp://%HOSTIP:%TFTPPORT//1094;mode=netascii"`) fails `data`: expected `a chunk of\r\n`, got
  `a chunk of\n`. Surl's `TftpProtocolServer` (ADR-0013) sends a file's bytes unchanged in either
  mode; RFC 1350 (and RFC 764's NVT ASCII it names) make netascii lines end CR LF on the wire.
- Measure what pinned upstream curl 8.21.0 writes for a netascii read (`Record-CurlExchange.ps1`)
  before pinning bytes; decide whether a write in netascii is converted back, and record the
  decision as an amendment to ADR-0013.
- `tsize` for a netascii read is the converted length, or the option is declined; decide which.

## Acceptance criteria

- [ ] `TftpProtocolServerTests` pins a netascii read's DATA bytes: LF sent as CR LF, CR as CR NUL.
- [ ] ADR-0013 records the netascii answer, marked "Decided by Claude under Stewart's delegation".
- [ ] The upstream test-case run on Windows passes test 1094, and it is added to
      `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Surl.Protocol.Tftp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
