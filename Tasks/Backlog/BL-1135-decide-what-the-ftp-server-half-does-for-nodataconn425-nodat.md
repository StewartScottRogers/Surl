---
id: BL-1135
title: Decide what the FTP server half does for NODATACONN425, NODATACONN421, NODATACONN150, PASVBADIP and RETRWEIRDO
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1135 — Decide what the FTP server half does for NODATACONN425, NODATACONN421, NODATACONN150, PASVBADIP and RETRWEIRDO

## Goal

An ADR, decided by Claude under Stewart's delegation, states for each of the FTP server commands
`NODATACONN425`, `NODATACONN421`, `NODATACONN150`, `PASVBADIP` and `RETRWEIRDO` exactly which bytes
the FTP upstream test-case server half sends, and when, so the task that builds them needs no
further choice.

## Context

- These commands keep upstream tests 1206, 1207, 1208, 270 and 126 skipped
  (`servercmd-unsupported:<command>`) on Windows, Linux and macOS (CI run 43629075978; local Windows
  reference run 2026-11-14).
- FILEFORMAT.md at tag `curl-8_21_0` documents two of them loosely: "`PASVBADIP` - makes PASV send
  back an illegal IP in its 227 response" and "`RETRWEIRDO` - Enable the "weirdo" RETR case when
  multiple response lines appear at once when a file is transferred". It does not document
  `NODATACONN425`, `NODATACONN421` or `NODATACONN150` at all.
- Evidence to use, in this order (ADR-0003; ADR-0080 says Surl is written from specifications,
  curl's documentation and measurement, not by translating upstream's test servers):
  1. each case's `<verify>` section: 1206 and 1207 (`--max-time %FTPTIME2 ... -P -`) expect exit 10,
     1208 expects exit 28; 270 uses `--ftp-skip-pasv-ip --disable-epsv`; 126's `<protocol>` and
     `<stdout>`;
  2. RFC 959 for what each reply code means;
  3. `tests/ftpserver.pl` at the tag, read only to learn what upstream's server is meant to do for
     each word (which reply code, whether the data connection is accepted, which lines are sent
     together), never copied;
  4. a measurement with a pinned build through `Record-CurlExchange.ps1` if a byte is still open.
- `%FTPTIME2` is already a case variable. The ADR also says which `FtpUpstreamTestCaseServerCommands`
  members the build adds, in `Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary`.
- Next free ADR number: ADR-0155 at filing (check `Documentation/Planning/Decisions/` first).

## Acceptance criteria

- [ ] An ADR under `Documentation/Planning/Decisions/` marked "Decided by Claude under Stewart's
      delegation" has one decision per command: the reply bytes, when they are sent, and what the half
      does with the data connection.
- [ ] Each decision names its evidence (case section, RFC 959 section, `ftpserver.pl` line read at
      tag `curl-8_21_0`, or a recorded measurement) and the curl version; none cites the Curl port.
- [ ] The ADR lists the cases each decision serves (1206, 1207, 1208, 270, 126) and the exit code
      each expects.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.

## Notes

## Log

- 2026-11-14: Created.
