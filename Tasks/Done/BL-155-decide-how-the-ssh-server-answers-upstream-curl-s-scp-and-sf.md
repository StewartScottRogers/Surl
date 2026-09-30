---
id: BL-155
title: Decide how the SSH server answers upstream curl's SCP and SFTP requests
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-154]
touches: [Documentation/Planning/Decisions]
requirement: FR-042
created: 2026-09-29
completed: 2026-09-29
---
# BL-155 — Decide how the SSH server answers upstream curl's SCP and SFTP requests

## Goal

An accepted ADR decides how `Surl.Protocol.Ssh` answers the SCP and SFTP requests upstream
curl 8.21.0 (libssh2/1.11.1) makes - path mapping, every request and its answer, the exposure
options, limits and refusals - so BL-164 to BL-166 can be built without a question.

## Context

- A canned server cannot get past SSH's key exchange, so this ADR decides from the
  specifications and from curl's and libssh2's documentation, each cited with its version.
  BL-164 to BL-166 test at channel level with byte scripts derived from this ADR, and BL-172
  proves them against the pinned build once surl serves `scp` and `sftp`; a disagreement there
  is a new task, never a changed expectation (ADR-0003).
- Sources: SCP as OpenSSH's `scp` speaks it (`scp -f` source mode, `scp -t` sink mode; the `C`,
  `D`, `E`, `T` control lines, `\0` acknowledgements, `\x01`/`\x02` errors); SFTP version 3,
  draft-ietf-secsh-filexfer-02 (the version libssh2 speaks); curl's manual
  (https://curl.se/docs/manpage.html) for `-Q`/`--quote` on SFTP (its command list), `-T`,
  `--append`, `-C -`, `--ftp-create-dirs`, `--create-file-mode`, and how an `scp://` or
  `sftp://` URL path is read (absolute path, and the `/~/` home-relative form).
- Decide:
  - How a URL path maps onto the served root (ADR-0031's data directory or in-memory root,
    `Surl.Content`'s `ContentStore`), including `/~/` and what `REALPATH` of `.` returns.
  - SCP: the exec command forms accepted; file mode and times sent for a download; what a
    directory, a hidden entry (ADR-0006 section 2: answered as missing), `/.surl` (ADR-0031
    decision 5) and a missing file get; an upload without `--allow-uploads`; one past
    `--max-filesize` (the partial upload deleted, ADR-0006 section 5); the error texts (nothing
    a peer may not learn, ADR-0006 section 3).
  - SFTP: the version answered; every request type and its status code and message, including
    the ones curl never sends (answered or `SSH_FX_OP_UNSUPPORTED`); attributes reported (size,
    permissions, times, uid/gid); `READDIR`'s long-name format; a listing without
    `--list-directories` (answered as missing); handle limits; the SFTP packet bound
    (`--max-message`); write semantics against `ContentStore`'s temporary-file-and-rename
    upload (open with truncate, append, resume at an offset); each `-Q` command's mapping
    (`rename`, `rm`, `mkdir`, `rmdir`, `chmod`, `chown`, `chgrp`, `ln`, `symlink`, `atime`,
    `mtime`, `statvfs`, `pwd`, whatever curl 8.21.0's manual lists) and what the content store
    cannot do (permissions, owners) is answered with.
  - The verbose notes the SCP and SFTP handlers write (ADR-0033), with no secret.
- Inputs: BL-154's ADR (transport, channels, authentication), ADR-0006, ADR-0015 (exposure
  options), ADR-0018, ADR-0031, ADR-0009 (what a listing holds).

## Acceptance criteria

- [x] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude
      under Stewart's delegation", decides every point in Context, each source cited with its
      version.
- [x] It lists the curl 8.21.0 command lines BL-172 must prove against surl (at least: an scp
      and an sftp download, an scp and an sftp upload, an sftp directory listing, each `-Q`
      command decided, `--append` and `-C -`), with the result the ADR expects for each.
- [x] `Documentation/Planning/Decisions/README.md` indexes the ADR.

## Notes

- ADR-0054 decides it from primary sources read at their tags: curl 8.21.0's `lib/vssh/libssh2.c`
  and `vssh.c` (tag `curl-8_21_0`), libssh2 1.11.1's `src/scp.c` and `src/sftp.c`, and `curl -M`
  from the pinned reference build (SHA-256 verified). No wire measurement: a canned server cannot
  pass SSH's key exchange, so BL-172 proves the ADR's 36 rows against the pinned build.
- Choices worth knowing: home is `/` (`REALPATH .` answers `/`); SFTP version 3, no extensions;
  SCP upload commits when the announced size has arrived (libssh2 sends EOF, not a final `\0`);
  permission/owner changes, `SYMLINK` and every `EXTENDED` (`statvfs@openssh.com` too) answer
  `OP_UNSUPPORTED`, so those `-Q` commands exit 21; a peer learns no disk sizes.
- `ContentStore` lacks four members the ADR needs (decision 14). Filed BL-230 (entry status,
  last-write-time setting, non-replacing rename) and BL-231 (random-access upload session), both
  touching `Surl.Content` only; BL-164 and BL-165 now depend on BL-230, BL-166 on BL-230 and BL-231.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0054 decides how the SSH server answers curl 8.21.0's SCP and SFTP requests; BL-230 and BL-231 filed for ContentStore
