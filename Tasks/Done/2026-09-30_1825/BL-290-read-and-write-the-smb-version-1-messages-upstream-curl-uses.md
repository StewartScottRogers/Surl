---
id: BL-290
title: Read and write the SMB version 1 messages upstream curl uses in Surl.Protocol.Smb
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Protocol.Smb.UnitLibrary, Surl.Protocol.Smb.UnitTests]
requirement: FR-050
created: 2026-09-30
completed: 2026-09-30
---
# BL-290 — Read and write the SMB version 1 messages upstream curl uses in Surl.Protocol.Smb

## Goal

`Surl.Protocol.Smb.UnitLibrary` reads the SMB version 1 requests upstream curl 8.21.0 sends and
writes their responses, inside NetBIOS session service framing, as internal types with no transport,
so the server tasks (BL-296 to BL-298) build on a codec already held to the quality gates.

## Context

- Groundwork decided by the specifications alone ([MS-CIFS] and [MS-SMB], NetBIOS session service
  RFC 1002 section 4.3); it needs no ADR and runs beside BL-283. Response field values that are
  server choices (dialect index, security mode, capabilities, error codes) are parameters here; BL-283's
  ADR and BL-296 choose them.
- The commands, from `lib/smb.c` at tag `curl-8_21_0` (read 2026-09-30): `SMB_COM_NEGOTIATE`
  (0x72), `SMB_COM_SESSION_SETUP_ANDX` (0x73, the NT LM 0.12 form with the LM and NT responses,
  account name, primary domain, native OS and native LAN manager as ASCII - curl sends no
  `FLAGS2_UNICODE`), `SMB_COM_TREE_CONNECT_ANDX` (0x75, path `\host\share` and service),
  `SMB_COM_NT_CREATE_ANDX` (0xA2), `SMB_COM_READ_ANDX` (0x2E), `SMB_COM_WRITE_ANDX` (0x2F),
  `SMB_COM_CLOSE` (0x04), `SMB_COM_TREE_DISCONNECT` (0x71); curl never chains a second AndX command
  (`SMB_COM_NO_ANDX_COMMAND`).
- What to build:
  - NetBIOS session message framing: type 0x00 with a 17-bit length (RFC 1002, and [MS-SMB] 2.1's
    direct-TCP use of it), read off an `IConnection` with the length checked against a maximum the
    caller gives before the message is read (ADR-0006: `--max-message` bounds an SMB message);
    session keep-alive (0x85) recognised; any other type reported;
  - the 32-byte SMB header (`0xFF 'SMB'`, command, status, flags, flags2, PID, TID, UID, MID) read
    and written, responses echoing TID, PID, UID and MID as [MS-CIFS] requires;
  - each request's parameter and data blocks decoded to a typed request, with word counts and byte
    counts validated, offsets inside the message, a chained AndX command reported rather than
    followed;
  - each response encoded: the negotiate response (NT LM 0.12 form, with the challenge and the domain
    name), session setup, tree connect, NT create (file ID, size, attributes, times from values given),
    read (data offset and length), write (count), close and tree disconnect, and an error response
    (header status, word count 0, byte count 0);
  - malformed input reported as a typed outcome, never an exception the caller must catch.
- Test bytes: hand-built from [MS-CIFS]'s field tables in the tests. What pinned upstream curl sends
  is recorded by BL-283 and replayed by BL-296 to BL-298, not here.
- Constraints: internal types, `InternalsVisibleTo` the tests (already in the csproj); complexity at
  most 10 per method; no package. The Curl port's SMB client code may be copied where it saves work;
  every expected byte still comes from the specifications here and from upstream curl later (ADR-0003).

## Acceptance criteria

- [x] Tests in `Surl.Protocol.Smb.UnitTests` decode each of the eight requests and encode each
      response in Context byte for byte against [MS-CIFS]'s layouts, and pass.
- [x] Tests show a NetBIOS length over the given maximum refused before the message is read, a
      truncated message, a bad signature, an inconsistent word or byte count, an offset outside the
      message and a chained AndX command each reported as its own outcome.
- [x] `dotnet build Surl.Protocol.Smb.UnitLibrary -warnaserror` is clean; the fast tests are green;
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Smb.UnitLibrary`.

## Notes

- If BL-283's ADR decides the SMB version 1 codec belongs in its own hand-built library rather than
  in `Surl.Protocol.Smb`, and this task has not started, `task-planner` re-plans it there.
- Built (2026-09-30): `SmbFrameReader` (NetBIOS framing off an `IConnection`, outcomes in
  `SmbFrameReadOutcome`), `SmbHeader`, `SmbRequestDecoder` (eight typed `Smb*Request` records,
  faults in `SmbRequestFault`, carried by `SmbRequestDecoding` with the header when readable),
  `SmbResponseEncoder` (every response framed, with `SmbNegotiateResponse` and
  `SmbNtCreateResponse` holding the server's values). 67 tests; `Measure-CodeQuality.ps1` reports
  100% line and branch, worst CRAP 8, 0 failing members.
- Defaults taken (spec-level, no ADR needed):
  - `--max-message` counts the NetBIOS length field (the SMB message, not the 4-byte NetBIOS
    header); 0 means no limit beyond 17 bits, matching `ExchangeLimits.MaxMessageBytes`. The
    flags byte's bits other than the length extension are ignored. Every frame's body is read
    before its type is judged, so a keep-alive or an unexpected type (reported with its type byte)
    leaves the stream at the next frame.
  - Response header = the request's, `Flags | SMB_FLAGS_REPLY`, `Flags2` unchanged, security
    features zero; session setup assigns the UID and tree connect the TID; all else echoed.
    AndX responses write `SMB_COM_NO_ANDX_COMMAND` and AndX offset 0 ([MS-CIFS] 2.2.3.4 has the
    receiver ignore it).
  - Strings are read and written as NUL-terminated OEM text encoded as UTF-8 (ASCII for what curl
    sends), since curl never sets `SMB_FLAGS2_UNICODE`.
  - READ_ANDX and WRITE_ANDX are accepted with or without the high-offset word (10/12 and 12/14
    words), as [MS-CIFS] allows. An NT create name has a trailing NUL trimmed whether or not
    `NameLength` counts it. Bytes after the byte block are ignored as padding.
  - Extra faults beyond the criteria: `MalformedString` (missing NUL or dialect buffer format) and
    `UnsupportedCommand` (any other command, header kept so the server can answer an error).
  - `Available` in the read and write responses is a parameter (it has meaning only for pipes).
  - The command dispatch is a table of decoders, not a `switch`: the `switch` over eight byte
    codes compiled to 16 branches and failed the coverage audit's complexity gate.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Protocol.Smb reads NetBIOS-framed SMB1 requests curl sends into typed requests and encodes every response, 100% covered
