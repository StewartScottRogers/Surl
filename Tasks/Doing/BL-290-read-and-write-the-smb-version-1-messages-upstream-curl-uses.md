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
completed:
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

- [ ] Tests in `Surl.Protocol.Smb.UnitTests` decode each of the eight requests and encode each
      response in Context byte for byte against [MS-CIFS]'s layouts, and pass.
- [ ] Tests show a NetBIOS length over the given maximum refused before the message is read, a
      truncated message, a bad signature, an inconsistent word or byte count, an offset outside the
      message and a chained AndX command each reported as its own outcome.
- [ ] `dotnet build Surl.Protocol.Smb.UnitLibrary -warnaserror` is clean; the fast tests are green;
      `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Smb.UnitLibrary`.

## Notes

- If BL-283's ADR decides the SMB version 1 codec belongs in its own hand-built library rather than
  in `Surl.Protocol.Smb`, and this task has not started, `task-planner` re-plans it there.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
