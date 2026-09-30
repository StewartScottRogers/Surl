# ADR-0054 — How the SSH server answers upstream curl's SCP and SFTP requests

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-155 (FR-042; FR-041 for SCP).
- **Amends:** nothing. [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) sections 2, 3
  and 5, [ADR-0015](ADR-0015-how-the-content-store-applies-the-exposure-options.md),
  [ADR-0018](ADR-0018-a-trailing-slash-after-a-file-and-a-served-root-that-is-not-a-full-path.md),
  [ADR-0031](ADR-0031-the-data-directory-and-in-memory-mode.md) decision 5 and
  [ADR-0051](ADR-0051-the-ssh-transport-host-keys-and-user-authentication.md) (transport,
  logins, channel limits) are applied as written. `Surl.Content` gains four members, listed in
  decision 14; no contract in `Surl.Protocol.Abstractions` changes.

## Context

ADR-0051 decided the SSH transport, the host keys, the logins and the channel limits. What was
left is what happens on a `session` channel once curl has logged in: which `exec` commands mean
SCP, how the `sftp` subsystem answers every SFTP version 3 request, how a path maps onto the
content store, what each exposure option and limit does, the verbose notes and what the pinned
build must prove, so BL-163 to BL-166 are built, and BL-172 proves them, without a question.

A canned server cannot get past SSH's key exchange (ADR-0051, "What upstream curl 8.21.0 sends"),
so nothing after `KEXINIT` can be recorded until surl itself serves `scp` and `sftp`. This ADR
therefore decides from the sources below, read on 2026-09-29, each the version the pinned build
carries: curl 8.21.0 with libssh2 1.11.1 (`UpstreamCurlBuilds.json`; the Windows reference build
`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`). BL-172 proves every
expectation against that build; a disagreement there is a new task, never a changed expectation
(ADR-0003).

### What upstream curl 8.21.0 sends over SCP (curl `lib/vssh/libssh2.c`, libssh2 `src/scp.c`)

- **The path.** curl percent-decodes the URL path (`Curl_getworkingpath`, `lib/vssh/vssh.c`). For
  `scp` a path starting `/~/` (and longer than 3 bytes) loses those 3 bytes and is sent relative
  (`scp://h/~/a.txt` names `a.txt`); every other path is sent as it is, absolute.
- **A download** is `libssh2_scp_recv2`: a `session` channel, an `exec` request (want-reply) with
  the command `scp -pf <path>` (the `p` because curl asks for the file's times), then one `\0`
  byte. libssh2 then reads one control line and requires it to start `T`; any other first byte
  (a `\x01` or `\x02` error line included) fails with `LIBSSH2_ERROR_SCP_PROTOCOL` and the text
  "Failed to recv file" (the server's own text is read and discarded). The `T` line must be
  `T<mtime> <usec> <atime> <usec>` LF with decimal fields separated by single spaces; libssh2
  answers `\0`, then requires a `C` line, `C<mode> <size> <name>` LF, the mode octal and wholly
  consumed, the size decimal, the name ignored; it answers `\0` and curl reads exactly `<size>`
  bytes. curl then frees the channel: **no acknowledgement is sent after the data**.
- **An upload** is `libssh2_scp_send64` with mode `--create-file-mode` (default 0644), the
  upload's size and times 0, so the command is `scp -t <path>` (no `p`) and no `T` line is sent.
  libssh2 waits for one byte, which must be `\0` ("Invalid ACK response from remote" otherwise),
  sends `C0%o <size> <basename>` LF (so `C0644 11 up.txt`), and waits for one byte again, which
  must be `\0` ("failed to send file" otherwise). curl then sends the data, **sends no `\0` after
  it**, sends `EOF` and waits for the channel to close; it reads nothing more. An upload of
  unknown size (`-T -`) is refused by curl itself ("SCP requires a known file size for upload",
  25) before any channel opens.
- **The path word** is shell-quoted by libssh2's `shell_quotearg` when its `quote_paths` session
  flag is set: runs of ordinary bytes inside `'...'`, runs of apostrophes inside `"..."`, and each
  `!` as `\!` outside both (`doesn't` becomes `'doesn'"'"'t'`); with the flag clear the path is
  appended as it is.
- **Exit codes** (`libssh2_session_error_to_CURLE`): `LIBSSH2_ERROR_SCP_PROTOCOL` is
  `CURLE_REMOTE_FILE_NOT_FOUND` (78); an `exec` request the server fails leaves a download with
  the channel-startup error libssh2 sets, which is not that one, so `CURLE_SSH` (79), while
  `scp_send` turns it into `LIBSSH2_ERROR_SCP_PROTOCOL`. For an upload curl maps 78 and 79 to
  `CURLE_UPLOAD_FAILED` (25). So every SCP download refusal is **78** with `curl: (78) Failed to
  recv file`, and every SCP upload refusal before the data is **25**.

### What upstream curl 8.21.0 sends over SFTP

- **Start** (`ssh_state_sftp_init`, `ssh_state_sftp_realpath`): the `sftp` subsystem,
  `SSH_FXP_INIT` version 3 (`LIBSSH2_SFTP_VERSION`; libssh2 truncates a higher server version to
  3), then `SSH_FXP_REALPATH` of `.`, whose answer curl keeps as the home directory. A failed
  `INIT` is `CURLE_FAILED_INIT` (2).
- **The path.** For `sftp`, `/~` and `/~/<rest>` become the home directory, a `/` if it does not
  end in one, and `<rest>`. A path ending in `/` is listed; any other is downloaded or uploaded.
- **Every `OPEN`** carries attributes with `PERMISSIONS` alone, `--create-file-mode` with the
  regular-file type bits (`0100644` by default), whatever its flags; every `MKDIR` carries
  `PERMISSIONS` `040755` (libssh2 `sftp_open`, `sftp_mkdir`).
- **A download**: `OPEN` with `READ`, then `STAT` of
  the path (not `FSTAT`): no `SIZE`, or size 0, means an unknown length, read to `EOF`. `-r` and
  `-C` seek, and `READ`s start at that offset. With `-R`, a `STAT` before the `OPEN` gives the
  file time. libssh2 asks for at most 30000 bytes per `READ` (`MAX_SFTP_READ_SIZE`) and pipelines
  several; a `DATA` shorter than asked for that is not followed by `EOF` fails with "Read Packet
  At Unexpected Offset" (libssh2 `src/sftp.c`, `sftp_read`).
- **A listing**: `OPENDIR`, `READDIR` until `STATUS EOF`, `CLOSE`. Without `-l` curl prints each
  entry's `longname` and LF; for an entry whose `PERMISSIONS` say `S_IFLNK` it first sends
  `READLINK` of `<path><name>` and appends ` -> <target>`. With `-l` it prints each `filename`
  and LF.
- **An upload** (`sftp_upload_init`): `OPEN` with `WRITE|CREAT|TRUNC`; with `--append`
  `WRITE|CREAT|APPEND`, writing from offset 0 (no seek); with `-C -` a `STAT` first - on failure
  the resume offset is 0 and the default flags are used, on success the offset is the size, the
  flags `WRITE` alone, and writes start at that offset. `WRITE`s are at most 30000 bytes (`MAX_SFTP_OUTGOING_SIZE`), in
  ascending offset order. With `--ftp-create-dirs`, an `OPEN` failing with `NO_SUCH_FILE`,
  `FAILURE` or `NO_SUCH_PATH` makes curl `MKDIR` each prefix of the path (`/new`, `/new/deep`;
  mode 0755), tolerating `FILE_ALREADY_EXISTS`, `FAILURE` and `PERMISSION_DENIED`, then `OPEN`
  once more.
- **Status to exit code** (`sftp_libssh2_error_to_CURLE`): `NO_SUCH_FILE`, `NO_SUCH_PATH` 78;
  `PERMISSION_DENIED`, `WRITE_PROTECT`, `LOCK_CONFLICT` 9; `NO_SPACE_ON_FILESYSTEM`,
  `QUOTA_EXCEEDED` 70; `FILE_ALREADY_EXISTS` 73; `DIR_NOT_EMPTY` 21; everything else, `FAILURE`
  and `OP_UNSUPPORTED` included, 79. Version 3 defines codes 0 to 8 only, so of these surl can
  send `NO_SUCH_FILE` (78), `PERMISSION_DENIED` (9) and the rest (79). A failed `WRITE` reaches
  curl through libssh2's session error instead (`sftp_send`): 79 whatever the status.
- **Quote commands** (`-Q`; curl's manual lists `atime`, `chgrp`, `chmod`, `chown`, `ln`,
  `mkdir`, `mtime`, `pwd`, `rename`, `rm`, `rmdir`, `symlink`; `sftp_quote` also takes
  `statvfs`). curl interprets them itself; a path in them is `/~/<rest>` rewritten to the home
  directory, `/` and `<rest>`, or a double- or single-quoted string, or a bare word:

  | `-Q` command | What curl sends |
  | --- | --- |
  | `pwd` | nothing: curl writes `257 "<URL path>" is current directory.` to its own header output |
  | `chmod <octal> <file>` | `SETSTAT` with `PERMISSIONS` only |
  | `chown <uid> <file>`, `chgrp <gid> <file>` | `STAT`, then `SETSTAT` with `UIDGID` only |
  | `atime <date> <file>`, `mtime <date> <file>` | `STAT`, then `SETSTAT` with `ACMODTIME` carrying the new time and the other time from the `STAT` |
  | `ln <source> <target>`, `symlink <source> <target>` | `SYMLINK` with the two paths in that order: a link at `<target>` pointing to `<source>` |
  | `mkdir <dir>` | `MKDIR`, mode 0755 |
  | `rename <source> <target>` | `RENAME`; libssh2 drops the overwrite flags below version 5 |
  | `rm <file>` | `REMOVE` |
  | `rmdir <dir>` | `RMDIR` |
  | `statvfs <path>` | `EXTENDED` `statvfs@openssh.com` |

  A failed request ends curl with `CURLE_QUOTE_ERROR` (21), unless the command starts with `*`,
  when curl carries on as if it had succeeded. A `STAT` that fails before a `SETSTAT` is 21 too.

## Decision

### 1. Paths

- **The home directory is `/`**, the served root, for every account (as FTP serves every account
  the same store, ADR-0052 decision 3). So `REALPATH .` answers `/`, and a relative path, SCP's
  and SFTP's alike, is resolved against `/`.
- **Canonical form.** A path is read as UTF-8; one that is not, or that holds a NUL, is answered
  as absent. Repeated `/` collapse, `.` segments drop, and each `..` removes the segment before
  it, stopping at `/`: `/../x` is `/x`, as POSIX resolves it. This differs on purpose from FTP,
  which answers a climb above `/` as absent (ADR-0052 decision 2): SFTP's `REALPATH` must name the
  path every other request then acts on, and SFTP clients join paths themselves (curl's `/~/x`
  quote form becomes `//x`). A trailing `/` is kept, so ADR-0018 still answers `/file.txt/` as
  absent. The empty path is `.`. `~` has no meaning.
- **Mapping.** The canonical path is percent-encoded segment by segment (every byte outside
  `A-Z a-z 0-9 - . _ ~` as `%HH`) and handed to `ContentStore.MapRequestPath`, so a name holding
  `%` means itself, and every rule of ADR-0006 section 2, ADR-0015, ADR-0018 and ADR-0031
  decision 5 applies exactly as over HTTP and FTP. A refused mapping (a device name, `a:b`, a link
  out of the root) is answered as absent.
- **Hidden entries and `/.surl`** are answered exactly as missing wherever something is read,
  stated or listed. Where something would be **created or replaced** there (an upload, `MKDIR`, a
  `RENAME` target), `ContentStore` answers `NotPermitted` (ADR-0015 decision 5, "the lesser
  leak", and `ContentChangeResult.NotPermitted`), and the SSH server answers it as every other
  `NotPermitted`: SFTP `PERMISSION_DENIED`, SCP `Permission denied`.

### 2. The `exec` command forms that mean SCP

BL-163's channel seam hands an `exec` request to the SCP handler only when its command, read as
UTF-8 and at most `--max-line` bytes, is exactly:

```
scp <options> [--] <path word>
```

- Words are separated by one or more spaces or tabs; leading and trailing blanks are ignored.
- `<options>`: one or more words each starting `-` and holding only `f`, `t`, `p`, `d`, `v`, in
  any order and grouping (`-pf`, `-p -f`, `-dt`). Exactly one of `f` (source: surl sends) and `t`
  (sink: surl receives) overall. `p` sends (`-f`) or honours (`-t`) the `T` line; `d` requires a
  `-t` target to be a directory; `v` is accepted and changes nothing.
- `<path word>`: exactly one, a concatenation of single-quoted runs (`'...'`, any byte but `'`),
  double-quoted runs (`"..."`, where `\` escapes only `"`, `\`, `$` and `` ` ``, and an unescaped
  `$` or `` ` `` is refused), a `\` escaping the next byte, and bare bytes other than blanks and
  `` ; & | < > ( ) $ ` * ? [ ] { } ~ # ! ' " \ ``. This takes libssh2's quoted form and its
  unquoted one, and refuses anything a shell would expand, since surl runs no shell.
- **Anything else is refused**: an `exec` of another program, `-r`, any other option, two paths
  or none, an unquoted special byte, bytes that are not UTF-8, a command past `--max-line`. The
  request is answered `SSH_MSG_CHANNEL_FAILURE` (curl: 79 for a download, 25 for an upload) and
  the channel is closed, with the note of decision 13. `-r` is refused because no curl command
  line can send it, so no pinned build could ever check an answer (ADR-0003); a later ADR can take
  it up.
- A second `exec` or `subsystem` on a channel that already runs one is `CHANNEL_FAILURE`.
- **Exit status** (RFC 4254 section 6.10): `exit-status` 0 when the transfer completed, 1 when
  anything was refused, sent before `EOF` and `CLOSE`.

### 3. SCP source mode (`-f`, curl's download)

After the `exec` is accepted surl waits for the client's `\0`, then, for a file:

```
T<mtime> 0 <mtime> 0\n        only with -p
C0644 <size> <name>\n
<size bytes of the file>\0
```

- `<mtime>` is the file's last write time in whole Unix seconds (UTC). The store keeps no access
  time, so `<atime>` repeats `<mtime>`. `0644` for every file: the store has no permission model,
  and `0644` is what `-rw-r--r--` says in the FTP listing (ADR-0052 decision 7). `<size>` is
  decimal; `<name>` is the last segment of the path as the client sent it. For `a.txt`, 12 bytes,
  written 2026-09-27 12:34:56 UTC: `T1790512496 0 1790512496 0\n`, `C0644 12 a.txt\n`.
- After each line surl waits for one byte: `\0` goes on; anything else, or `EOF`, ends the
  transfer (exit status 1). After the data and its `\0` surl reads one more byte or `EOF` (curl
  sends none and frees the channel) and ends with exit status 0 either way.
- The file is read through `ContentStore.CopyFileBytesAsync` with its length from
  `GetFileStatus` taken before the `C` line; a file that shrinks meanwhile ends the channel after
  the bytes there are, without the trailing `\0`, exit status 1.
- **Refusals**, each one line then exit status 1, `EOF` and `CLOSE` (curl: 78, "Failed to recv
  file"), `<path>` being the path word as the client sent it after unquoting, rendered as ADR-0006
  section 3 renders a peer's bytes:

  | Case | Line sent |
  | --- | --- |
  | Missing, hidden, under `/.surl`, a refused mapping, or `/name/` naming a file | `\x01scp: <path>: No such file or directory\n` |
  | A directory | `\x01scp: <path>: not a regular file\n` |
  | The store fails to read | `\x01scp: <path>: read error\n` (the exception message only in the note) |

  These follow the shape OpenSSH's `scp` writes (`scp: <path>: <reason>`); the words are
  surl's, and name nothing but the client's own path (ADR-0006 section 3).

### 4. SCP sink mode (`-t`, curl's upload)

- surl sends `\0` at once, then reads control lines, each ending at LF and at most `--max-line`
  bytes with it:
  - `T<mtime> <usec> <atime> <usec>` (decimal) is kept for the next file and answered `\0`;
  - `C<mode> <size> <name>`: `<mode>` 1 to 6 octal digits, at most `07777`; `<size>` 1 to 19
    decimal digits within a `long`; `<name>` one path segment (no `/`, not `.`, `..` or empty);
  - `D` and `E` lines (a directory, which only `-r` sends) and anything else are protocol errors.
- **The target**: when the path word names an existing directory the file is `<path>/<name>`, as
  OpenSSH's sink does; otherwise it is the path word itself and `<name>` is not used. With `-d`
  a path word that is not an existing directory is refused.
- **The checks, in order, all on the `C` line and before any data is read**, each refused with one
  line, exit status 1, `EOF`, `CLOSE`, the data never read (curl: 25, "failed to send file"):

  | Case | Line sent |
  | --- | --- |
  | A malformed or unknown line, `D`, `E`, a bad mode, size or name | `\x01scp: protocol error: <what>\n` (`bad mode`, `bad size`, `unexpected filename`, `received directory without -r`, `line too long`, `unexpected line`) |
  | `--allow-uploads` off | `\x01scp: <target>: Permission denied\n` |
  | A refused mapping, or the target's directory does not exist | `\x01scp: <target>: No such file or directory\n` |
  | `-d` and the path word is not a directory | `\x01scp: <path>: Not a directory\n` |
  | Hidden, under `/.surl`, or otherwise `NotPermitted` by the store | `\x01scp: <target>: Permission denied\n` |
  | `<size>` above `--max-filesize` (when it is not 0) | `\x01scp: <target>: File too large\n` |

- **Otherwise** surl sends `\0` and streams exactly `<size>` bytes into
  `ContentStore.WriteUploadAsync` (a temporary dot-file renamed into place, ADR-0015 decision 6;
  the store counts against `--max-filesize` again). The file is committed once the `<size>`th
  byte arrives: libssh2 sends no `\0` after the data, only `EOF`. surl then reads one byte or
  `EOF`: a `\0` (OpenSSH's client sends one) or `EOF` is answered `\0`, exit status 0; another byte
  leaves the committed file in place, as OpenSSH's sink (which writes in place) does, and ends
  with exit status 1. A `T` line's `<mtime>` is then applied with decision 14's
  `SetLastWriteTime`; its `<atime>` is not kept. curl sends no `T` line.
- **Data that ends early** (`EOF` or a channel close before `<size>` bytes): the stream reports
  the failure to the store, which deletes the temporary file (ADR-0006 section 5); nothing is
  committed and the target is untouched.
- **After the data, curl reads nothing** (Context), so a store failure there (`IOException`, the
  in-memory file system full, ADR-0031 decision 4) cannot reach curl's exit code: surl sends
  `\x01scp: <target>: write error\n`, exit status 1, and the note carries the exception message.
- `<mode>` is not kept (no permission model); the note says so.

### 5. The SFTP session

- **Start.** A `subsystem` request named exactly `sftp` starts it (answered `CHANNEL_SUCCESS` when
  a reply is wanted); any other subsystem is `CHANNEL_FAILURE`. The first packet must be
  `SSH_FXP_INIT`: with version 3 or higher surl answers `SSH_FXP_VERSION` 3 with no extension
  pairs, `00 00 00 05 02 00 00 00 03`; below 3, or any other first packet, ends the subsystem
  (below). A second `INIT` is a malformed packet.
- **Framing.** Every packet is `uint32 length`, `byte type`, then its fields
  (draft-ietf-secsh-filexfer-02 section 3). Requests are answered one at a time in the order they
  arrive, each reply carrying its request's `id`; the channel window (ADR-0051 decision 9) is the
  back-pressure, so pipelined requests cost no memory beyond it.
- **The bound.** `length + 4` above `--max-message` (1 MiB by default, 0 no limit, ADR-0006
  section 1: an SFTP packet is a framed message), or a `length` below 5, ends the subsystem before
  the body is read. curl's largest packet is a `WRITE` of 30000 data bytes, 30029 bytes in all
  with surl's 4-byte handles, so a `--max-message` below 30029 breaks curl's uploads; that is the
  operator's choice.
- **A packet that is framed but does not parse** (a string running past its packet, a field
  missing, bytes left over) is answered `STATUS BAD_MESSAGE` `Bad message`, and the session goes
  on. An unknown `type` is `OP_UNSUPPORTED` (decision 6).
- **Ending.** The client's `EOF` ends the session with exit status 0; a framing failure or a
  refused `INIT` with exit status 1 and no reply; either way surl sends `EOF` and `CLOSE`, every
  handle is released, and every upload not yet committed at `CLOSE` is discarded (decision 9).
- **Handles** are 4 bytes, a big-endian counter per session, never reused. At most **100** open
  per session (10 sessions per connection, ADR-0051 decision 9); the 101st `OPEN` or `OPENDIR` is
  `FAILURE` `Too many open handles`. A handle surl never issued, or already closed, is `FAILURE`
  `Invalid handle` (version 3 has no `INVALID_HANDLE`).

### 6. `STATUS` codes and messages

Every `SSH_FXP_STATUS` carries `id`, the code, a fixed English message from this table and the
language tag `en` (draft-02 section 7); no message names a path, a host directory or an exception
(ADR-0006 section 3). curl prints its own text for a code, never the message (Context).

| Code | Message(s) |
| --- | --- |
| 0 `OK` | `Success` |
| 1 `EOF` | `End of file` |
| 2 `NO_SUCH_FILE` | `No such file` |
| 3 `PERMISSION_DENIED` | `Permission denied` |
| 4 `FAILURE` | `File already exists`, `Directory not empty`, `Is a directory`, `Not a directory`, `Not a symbolic link`, `File too large`, `Handle not open for writing`, `Handle not open for reading`, `Too many open handles`, `Invalid handle`, `Write failed`, `Read failed` |
| 5 `BAD_MESSAGE` | `Bad message` |
| 8 `OP_UNSUPPORTED` | `Operation unsupported`, `Permissions and owners are not kept`, `Symbolic links cannot be created`, `Extension not supported` |

Codes 6 `NO_CONNECTION` and 7 `CONNECTION_LOST` are client-side and never sent. `OK` for id 1 is
`00 00 00 1A 65 00 00 00 01 00 00 00 00 00 00 00 07 53 75 63 63 65 73 73 00 00 00 02 65 6E`.

### 7. Attributes and the long name

- **Attributes** for every file and directory: `flags` `0x0000000D` (`SIZE`, `PERMISSIONS`,
  `ACMODTIME`); `size` the file's length, 0 for a directory; `permissions` `0100644` for a file,
  `040755` for a directory, the same modes ADR-0052's `ls -l` lines show; `atime` and `mtime`
  both the last write time in whole Unix seconds, clamped to `0` .. `0xFFFFFFFF` (the store keeps
  no access time). No `UIDGID`: the store has no owners. No `S_IFLNK` is ever reported: a link
  inside the root is either followed and reported as its target (`--follow-symlinks`) or answered
  as missing (ADR-0015 decision 2), so curl never sends `READLINK` for a listing.
- `ATTRS` for `a.txt` (12 bytes, 2026-09-27 12:34:56 UTC), id `n`:
  `00 00 00 1D 69 <n> 00 00 00 0D 00 00 00 00 00 00 00 0C 00 00 81 A4 6A B9 0D 70 6A B9 0D 70`.
- **The long name** in `READDIR` is ADR-0052 decision 7's `LIST` line without its CRLF, from the
  same entry and the same injected `TimeProvider`: `-rw-r--r-- 1 surl surl <size> <date> <name>`
  for a file, `drwxr-xr-x 1 surl surl 0 <date> <name>` for a directory, `<size>` right-aligned in
  12 columns, `<date>` `MMM dd HH:mm` within 180 days, else `MMM dd  yyyy`, UTC. So a curl listing
  over SFTP reads line for line as one over FTP:
  `-rw-r--r-- 1 surl surl           12 Sep 27 12:34 a.txt`.

### 8. Reads, stats and listings

| Request | Answer |
| --- | --- |
| `REALPATH <path>` | `NAME`, count 1, `filename` and `longname` both the canonical path (decision 1) without a trailing `/` (`/` for the root), attributes `flags` 0. Existence is not checked, so it tells a peer nothing. `REALPATH .` for id `n`: `00 00 00 17 68 <n> 00 00 00 01 00 00 00 01 2F 00 00 00 01 2F 00 00 00 00` |
| `STAT <path>`, `LSTAT <path>` | `ATTRS` (decision 7) for a file or directory; `NO_SUCH_FILE` for anything answered as absent. `LSTAT` equals `STAT`: no link is ever reported as one |
| `FSTAT <handle>` | a read handle: the file's attributes now; a write handle: `size` the upload's current length, times the injected clock's now; a directory handle: the directory's |
| `OPEN <path> READ` | a file: `HANDLE`; a directory: `FAILURE` `Is a directory` (curl: 79); absent: `NO_SUCH_FILE` (78). `OPEN`'s attributes are ignored |
| `READ <handle> <offset> <len>` | `DATA` of `min(len, bytes to end of file, 261120)` bytes from `offset`, through `CopyFileBytesAsync` with `ContentByteRange.Select`; `offset` at or past the end: `STATUS EOF`. The data is never shorter than asked for except at the end of the file, since libssh2 fails a short read that is not followed by `EOF`; 261120 keeps the `DATA` packet under libssh2's 256 KiB incoming bound. A file gone since `OPEN` is `NO_SUCH_FILE`; a store failure `FAILURE` `Read failed`. A write handle opened without `READ` is `FAILURE` `Handle not open for reading` |
| `OPENDIR <path>` | `--list-directories` off: `NO_SUCH_FILE` whatever is there (ADR-0006 section 2; curl: 78). On: a directory's `HANDLE`, its listing read once now from `ContentStore.ListDirectory` (ADR-0009: dot-files, `/.surl` and unfollowed links left out, ordinal order); a file: `FAILURE` `Not a directory`; absent: `NO_SUCH_FILE` |
| `READDIR <handle>` | `NAME` with the next entries - at most 100, and never a packet past 262144 bytes - then `STATUS EOF` on the call after the last, and again on any later call. No `.` or `..` entries: ADR-0009 lists none, and ADR-0052's `LIST` sends none |
| `READLINK <path>` | an existing entry: `FAILURE` `Not a symbolic link` (the store reports no link, and a link's target text could name a host path, ADR-0006 section 3); absent: `NO_SUCH_FILE` |
| `CLOSE <handle>` | `OK`, releasing it; for a write handle decision 9's commit decides the status |

### 9. Writes

- **Every request that writes needs `--allow-uploads`**: without it `OPEN` with `WRITE`, `REMOVE`,
  `RENAME`, `MKDIR`, `RMDIR` and a `SETSTAT` or `FSETSTAT` that would change something are answered
  `PERMISSION_DENIED` (curl: 9 for an upload, 21 for a `-Q` command) before anything else is
  looked at. ADR-0006 section 2 lists "SFTP and SCP writes" under that option.
- **`OPEN` with `WRITE`**, `pflags` read as draft-02 section 6.3 defines them: `TRUNC` or `EXCL`
  without `CREAT` is `BAD_MESSAGE`; a bit above `EXCL` is `OP_UNSUPPORTED`. Then, in order:

  | Case | Answer |
  | --- | --- |
  | A refused mapping, or the target's directory does not exist | `NO_SUCH_FILE` (so `--ftp-create-dirs` makes the directories, Context) |
  | Hidden, under `/.surl`, or a trailing `/` | `PERMISSION_DENIED` (decision 1) |
  | A directory | `FAILURE` `Is a directory` |
  | `CREAT` and `EXCL`, and a file is there | `FAILURE` `File already exists` |
  | No `CREAT` and nothing is there | `NO_SUCH_FILE` |
  | Otherwise | `HANDLE`: an upload (decision 14's `ContentUploadSession`) holding nothing when `TRUNC` is set or no file is there, else a copy of the file's bytes |

  `OPEN`'s `PERMISSIONS` (curl sends `--create-file-mode`) are not kept, and the note says so:
  refusing them would refuse every curl upload, and ignoring a requested creation mode is what a
  server's `umask` already does. `READ` with `WRITE` is served: `READ` sees the upload's bytes.
- **`WRITE <handle> <offset> <data>`**: an `APPEND` handle writes at the upload's end whatever
  the offset (curl's `--append` writes from offset 0 and expects the server to append, Context);
  any other writes at `offset`, extending with zero bytes past the end. `OK` when written. A write
  that would take the upload past `--max-filesize` is `FAILURE` `File too large`: the upload is
  discarded (the temporary file deleted, ADR-0006 section 5), the target untouched, and every
  later request on the handle but `CLOSE` gets the same answer (curl: 79). A store failure is
  `FAILURE` `Write failed`, with the same effect. A read handle is `FAILURE` `Handle not open for
  writing`.
- **`CLOSE` of a write handle commits** the upload (the temporary file renamed over the target)
  and applies any `ACMODTIME` an `FSETSTAT` set: `OK`, or `FAILURE` `File too large` / `Write
  failed` for an upload already discarded or failing to commit. The handle is released either
  way. An upload whose session ends before `CLOSE` is discarded, never committed.
- So curl's four uploads are: `-T` (`TRUNC`, writes from 0), `--append` (`APPEND`, each write at
  the end), `-C -` on an existing file (a copy of its bytes, writes from its length), and `-C -`
  on a missing one (`STAT` fails, then as `-T`).

### 10. Changing the tree

| Request | Answer (with `--allow-uploads`) |
| --- | --- |
| `REMOVE <path>` | a file: `OK` (`ContentStore.DeleteFile`); a directory: `FAILURE` `Is a directory`; absent: `NO_SUCH_FILE` |
| `RENAME <old> <new>` | `OK` through decision 14's `RenameEntryWithoutReplacing`: draft-02 section 6.5 makes an existing `<new>` an error, so any entry there is `FAILURE` `File already exists`; `<old>` absent, or `<new>`'s directory missing: `NO_SUCH_FILE`; `<new>` hidden or under `/.surl`, the root, a directory into itself: `PERMISSION_DENIED` |
| `MKDIR <path> <attrs>` | `OK` (`CreateDirectory`); an entry there: `FAILURE` `File already exists` (curl's `--ftp-create-dirs` tolerates it); the parent missing: `NO_SUCH_FILE`; hidden or under `/.surl`: `PERMISSION_DENIED`. The attributes are not kept |
| `RMDIR <path>` | an empty directory: `OK` (`RemoveEmptyDirectory`); not empty (hidden entries count): `FAILURE` `Directory not empty`; a file: `FAILURE` `Not a directory`; absent: `NO_SUCH_FILE`; the root: `PERMISSION_DENIED` |
| `SETSTAT <path> <attrs>`, `FSETSTAT <handle> <attrs>` | below |
| `SYMLINK <link> <target>` | `OP_UNSUPPORTED` `Symbolic links cannot be created`: the store creates none, and a link a peer could plant would alias paths the operator's exposure options were set against |
| `EXTENDED <name> ...` (`statvfs@openssh.com` included) | `OP_UNSUPPORTED` `Extension not supported`: `VERSION` advertises none, and `statvfs` would tell a peer the host's disk sizes |
| any other type (`101` to `201` sent by a client included) | `OP_UNSUPPORTED` `Operation unsupported` |

**`SETSTAT` and `FSETSTAT`**, all or nothing, in this order: the path absent (or the handle
invalid): `NO_SUCH_FILE` (`FAILURE` `Invalid handle`); `flags` holding `UIDGID`, `PERMISSIONS` or
`EXTENDED`: `OP_UNSUPPORTED` `Permissions and owners are not kept`, nothing changed (the FTP server
answers `SITE CHMOD` the same way, ADR-0052 decision 8); `flags` 0: `OK`, nothing changed;
`--allow-uploads` off: `PERMISSION_DENIED`; then

- `SIZE` on a path: the file is truncated or zero-extended to it through an upload holding a copy
  of its bytes, committed at once (within `--max-filesize`); on a write handle: the upload's
  length; on a read or directory handle: `FAILURE` `Handle not open for writing`; on a directory
  path: `FAILURE` `Is a directory`;
- `ACMODTIME`: `mtime` becomes the entry's last write time (decision 14's `SetLastWriteTime`, for
  a file or a directory; on a write handle, applied at its `CLOSE`). `atime` is not kept: the
  store has one time per entry.

`OK` once all are applied.

### 11. What each of curl's `-Q` commands gets

With `--allow-uploads` unless the row says otherwise; "then the transfer" means curl goes on to
the URL's transfer.

| `-Q` command | Requests and answers | curl |
| --- | --- | --- |
| `pwd` | none | 0, then the transfer |
| `chmod 0600 /a.txt` | `SETSTAT PERMISSIONS`: `OP_UNSUPPORTED` | 21; with `*chmod`, 0 and the transfer |
| `chown 1000 /a.txt`, `chgrp 1000 /a.txt` | `STAT`: `ATTRS`; `SETSTAT UIDGID`: `OP_UNSUPPORTED` | 21 |
| `mtime <date> /a.txt` | `STAT`; `SETSTAT ACMODTIME`: `OK`, the file's time is `<date>` | 0; without `--allow-uploads` 21 |
| `atime <date> /a.txt` | `STAT`; `SETSTAT ACMODTIME` carrying the unchanged `mtime`: `OK`, nothing visible changes | 0; without `--allow-uploads` 21 |
| `ln /a.txt /l.txt`, `symlink ...` | `SYMLINK`: `OP_UNSUPPORTED` | 21 |
| `mkdir /x` | `MKDIR`: `OK` | 0; an existing `/x` 21 |
| `rename /a.txt /c.txt` | `RENAME`: `OK` | 0; an existing `/c.txt` 21 |
| `rm /a.txt` | `REMOVE`: `OK` | 0; a missing file 21 |
| `rmdir /x` | `RMDIR`: `OK` | 0; not empty 21 |
| `statvfs /` | `EXTENDED statvfs@openssh.com`: `OP_UNSUPPORTED` | 21; with `*statvfs`, 0 |

### 12. Limits (ADR-0006, applied to SCP and SFTP)

| Limit | Value | Past it |
| --- | --- | --- |
| `exec` command | `--max-line` (8192 bytes) | `CHANNEL_FAILURE` (decision 2) |
| SCP control line, LF included | `--max-line` | `\x01scp: protocol error: line too long\n`, exit status 1 |
| SCP upload | `--max-filesize` (100 MiB) against the `C` line's size | refused before any data (decision 4) |
| SFTP packet, length field included | `--max-message` (1 MiB) | the subsystem ends, exit status 1 (decision 5) |
| SFTP upload | `--max-filesize` against the upload's length after each `WRITE` or `SIZE` | `FAILURE` `File too large`, the upload discarded (decision 9) |
| Open SFTP handles | 100 per session | `FAILURE` `Too many open handles` |
| `READ` reply | 261120 bytes | the rest on the next `READ` |
| `READDIR` batch | 100 entries, 262144-byte packet | the rest on the next `READDIR` |
| Sessions per connection, windows, idle timeout, `--max-time` | ADR-0051 decision 9, ADR-0006 section 1 | as they decide |

`--max-line`, a line-protocol limit in ADR-0006 section 1, now also bounds SCP's `exec` command
and control lines: they are command lines, and no other limit bounds them below `--max-message`.

### 13. The verbose notes (ADR-0033)

Written with `IExchangeLog.Note` at `verbose` and above, one line each, every peer byte (a
command, a path) escaped as ADR-0006 section 3 says. Never in a note: file contents, a password,
a key (ADR-0051 decision 10).

| When | Note |
| --- | --- |
| An `exec` refused | `SCP command refused: <reason>: <command>` (`not an scp command`, `-r is not served`, `unknown option -<c>`, `needs exactly one path`, `unquoted <byte>`, `not UTF-8`, `past --max-line`) |
| A download sent | `SCP download <path>: sent <n> bytes` |
| An upload committed | `SCP upload <target>: received <n> bytes` (and `, mode <octal> not kept`; `, modification time set` after a `T` line) |
| A refusal | `SCP <download\|upload> <path> refused: <reason>` (the line sent, and for a store failure the exception message) |
| An upload abandoned | `SCP upload <target> abandoned after <n> of <size> bytes` |
| A subsystem refused | `SSH subsystem refused: <name>` |
| An SFTP session starts or ends | `SFTP session started: client version <v>, answering 3`; `SFTP session ended: <client EOF\|malformed packet\|packet past --max-message\|client version <v> below 3>` (with `, <k> uploads discarded` when any were) |
| `OPEN`, `OPENDIR`, `REMOVE`, `RENAME`, `MKDIR`, `RMDIR`, `SETSTAT`, `FSETSTAT`, `SYMLINK`, `EXTENDED`, and any request answered other than `OK`, `EOF` or data | `SFTP <REQUEST> <path or handle's path> [<flags>] -> <STATUS NAME>`, and for a refusal `: <reason>` (`uploads are off (--allow-uploads)`, `listings are off (--list-directories)`, `answered as absent`, `past --max-filesize`, the store's exception message) |
| `OPEN` with `CREAT` that creates a file, or `MKDIR` that creates a directory, carrying `PERMISSIONS` (curl's always do) | `SFTP <REQUEST> <path>: permissions <octal> not kept` |
| `CLOSE` | `SFTP CLOSE <path>: read <n> bytes`, `wrote <n> bytes`, or `upload discarded: <reason>` |

`READ`, `WRITE`, `READDIR`, `STAT`, `LSTAT`, `FSTAT` and `REALPATH` that succeed write no note
(the `CLOSE` note counts the bytes); `--trace` carries the channel bytes, which after `NEWKEYS` are
ciphertext (ADR-0051 decision 10).

### 14. What `Surl.Content` must gain

`ContentStore` already reads, stats files, lists, uploads through a temporary file
(`WriteUploadAsync`, `AppendUploadAsync`), deletes, renames (replacing a file), creates and removes
directories (BL-226, for FTP). This ADR needs four more members, each governed by ADR-0015's
exposure rules, each write needing `AllowUploads`, and each built and tested in `Surl.Content`
(the protocol tasks touch `Surl.Protocol.Ssh` only):

1. **`GetEntryStatus(ContentPathMapping)`**: the kind, a file's length and the last write time of
   a file **or a directory**, `null` for nothing or a hidden entry. `GetFileStatus` answers files
   only, and `STAT` of a directory needs its time (decision 7).
2. **`SetLastWriteTime(ContentPathMapping, DateTimeOffset)`**, answering a `ContentChangeResult`
   (`NotPermitted` with uploads off, `Absent` for nothing or a hidden entry), for a file or a
   directory; the seam gains `IContentFileSystem.SetLastWriteTimeUtc(string, DateTimeOffset)` as a
   default member throwing `NotSupportedException` (ADR-0015 decision 7's pattern), which
   `DiskContentFileSystem` and `InMemoryContentFileSystem` implement. For `ACMODTIME` (decision 10)
   and an SCP `T` line (decision 4).
3. **`RenameEntryWithoutReplacing(ContentPathMapping, ContentPathMapping)`**: `RenameEntry`'s rules,
   except that any entry at the destination is `ContentChangeResult.Exists`; the seam gains
   `MoveFileWithoutReplacing(string, string)` (default throwing), which the disk implements with
   `File.Move(..., overwrite: false)` so the check and the move are one step. For `RENAME`
   (decision 10).
4. **A random-access upload**: `OpenUpload(ContentPathMapping, ContentUploadOpening)` answering a
   result (`Opened`, `NotPermitted`, `NoSuchDirectory`, `IsADirectory`, `Exists`, `Absent`) and,
   when opened, a `ContentUploadSession` with `Length`, `WriteAtAsync(offset, bytes)` (zero-filling
   a gap), `ReadAtAsync(offset, buffer)`, `SetLengthAsync(length)`, `CommitAsync()` (the temporary
   file renamed over the target, answering `Written` or the failure) and `DisposeAsync()`
   (discarding an uncommitted upload). `ContentUploadOpening` says whether to start from a copy of
   the existing bytes or empty, whether to create a missing file, and whether an existing one is
   refused. Any write or `SetLengthAsync` taking `Length` past `MaxUploadBytes` ends the session as
   `TooLarge` and deletes the temporary file. The seam gains `OpenFileForAsyncReadWrite(string)`
   (default throwing) returning a seekable, readable, writable stream; the in-memory file system's
   write stream gains seeking and `SetLength`, still within `MaxTotalBytes`. For `OPEN` with
   `WRITE`, `WRITE`, `READ` on a write handle, and `SIZE` (decisions 9 and 10).

SCP needs only item 2 beyond today's `WriteUploadAsync`. Which task builds each item, and the
dependencies of BL-164 (item 2), BL-165 (item 1) and BL-166 (items 1 to 4) on them, are filed on
the board separately.

### 15. Who builds what

| Work | Task |
| --- | --- |
| The `exec` grammar of decision 2 as the channel seam's dispatch, `subsystem sftp`, `exit-status` | BL-163 |
| Decisions 1, 3 and 4, SCP's rows of decisions 12 and 13 | BL-164 |
| Decisions 5 to 8, the read side of decision 13 | BL-165 |
| Decisions 9 to 11, the write side of decision 13 | BL-166 |
| Decision 14 | tasks in `Surl.Content`, filed separately |
| The `ssh` category (ADR-0051 decision 12) also holds `--directory`, `--allow-uploads`, `--list-directories`, `--follow-symlinks`, `--serve-dot-files`, `--max-filesize`, `--max-line` and `--max-message`; the `--aihelp` prose and example for `scp` and `sftp` | BL-171 |
| Decision 16 | BL-172 |

### 16. What BL-172 must prove with the pinned build

Each against a live `surl` on loopback, with `HOME` and `USERPROFILE` pointed at a temporary
directory (ADR-0051 decision 8). `S` is `surl --directory <d> --throwaway-hostkey --user
tester:secret -v` serving `scp://127.0.0.1:<p>/` and `sftp://127.0.0.1:<q>/`, where `<d>` holds
`a.txt` (`hello world` LF, 12 bytes), `dir/b.txt` and `.hidden.txt`; `C` is `curl -sS
--hostpubsha256 <H> -u tester:secret`, `<H>` read from surl's fingerprint note; `up.txt` is the
local file `hello again` (11 bytes, no LF); `P` is `scp://127.0.0.1:<p>` and `Q`
`sftp://127.0.0.1:<q>`. Every row starts from a fresh `<d>`, and a pair of commands in one cell
runs in that order against the same `<d>`.

| surl | curl | Exit and result |
| --- | --- | --- |
| `S` | `C P/a.txt` | 0, stdout `hello world` LF |
| `S` | `C P/~/a.txt` | 0, the same |
| `S` | `C P/missing.txt`, `C P/.hidden.txt`, `C P/dir` | 78, `curl: (78) Failed to recv file` |
| `S` | `C -T up.txt P/new.txt` | 25, `curl: (25) failed to send file`; no `new.txt` |
| `S --allow-uploads` | `C -T up.txt P/new.txt` | 0; `new.txt` is `up.txt` |
| `S --allow-uploads` | `C -T up.txt P/nodir/new.txt` | 25 |
| `S --allow-uploads --max-filesize 4` | `C -T up.txt P/new.txt` | 25; no `new.txt` |
| `S` | `C Q/a.txt`, and `C Q/~/a.txt` | 0, stdout `hello world` LF |
| `S` | `C Q/missing.txt`, `C Q/.hidden.txt`, `C Q/.surl/lock` | 78, `curl: (78) Could not open remote file for reading: No such file or directory` |
| `S` | `C Q/dir` | 79 (`Is a directory`) |
| `S` | `C -r 0-4 Q/a.txt` | 0, stdout `hello` |
| `S` | `C -C - -o part.txt Q/a.txt`, `part.txt` holding `hello` | 0; `part.txt` is `hello world` LF |
| `S` | `C -R -o out.txt Q/a.txt` | 0; `out.txt`'s modification time is `a.txt`'s |
| `S` | `C Q/dir/` | 78, `curl: (78) Could not open directory for reading: No such file or directory` |
| `S --list-directories` | `C Q/dir/` | 0, one line per entry in decision 7's long-name form, LF-ended |
| `S --list-directories` | `C -l Q/` | 0, stdout `a.txt` LF `dir` LF (no `.hidden.txt`, no `.surl`) |
| `S` | `C -T up.txt Q/new.txt` | 9; no `new.txt` |
| `S --allow-uploads` | `C -T up.txt Q/new.txt`, and with `--create-file-mode 0600` | 0; `new.txt` is `up.txt` (the mode noted, not kept) |
| `S --allow-uploads` | `C -T up.txt Q/nodir/new.txt` | 78 |
| `S --allow-uploads` | `C --ftp-create-dirs -T up.txt Q/new/deep/b.txt` | 0; `new/deep/b.txt` is `up.txt` |
| `S --allow-uploads` | `C -a -T up.txt Q/a.txt` | 0; `a.txt` is `hello world` LF `hello again` |
| `S --allow-uploads` | `C -C - -T up.txt Q/c.txt`, `c.txt` holding `hello`; and with no `c.txt` | 0 both; `c.txt` is `up.txt` |
| `S --allow-uploads --max-filesize 4` | `C -T up.txt Q/new.txt` | 79; no `new.txt` |
| `S --allow-uploads` | `C -Q "rename /a.txt /c.txt" Q/c.txt` | 0, stdout `hello world` LF |
| `S --allow-uploads` | `C -Q "rename /~/a.txt /~/c.txt" Q/c.txt` | 0, the same (`//a.txt` canonicalised) |
| `S --allow-uploads` | `C -Q "rename /a.txt /dir/b.txt" Q/a.txt` | 21 (`/dir/b.txt` exists) |
| `S --allow-uploads --list-directories` | `C -Q "rm /a.txt" -l Q/` | 0, stdout `dir` LF |
| `S --allow-uploads --list-directories` | `C -Q "mkdir /x" Q/x/`, then `C -Q "rmdir /x" Q/x/` | 0 with empty stdout, then 78 (`/x` removed before the listing) |
| `S --allow-uploads` | `C -Q "chmod 0600 /a.txt" Q/a.txt`; `chown 1000`, `chgrp 1000` | 21 each |
| `S --allow-uploads` | `C -Q "*chmod 0600 /a.txt" Q/a.txt` | 0, stdout `hello world` LF |
| `S --allow-uploads` | `C -Q "ln /a.txt /l.txt" Q/a.txt`; `symlink` | 21 each |
| `S --allow-uploads` | `C -Q "mtime \"Sun, 27 Sep 2026 12:34:56 GMT\" /a.txt" -R -o out.txt Q/a.txt` | 0; `out.txt`'s time is 2026-09-27 12:34:56 UTC |
| `S --allow-uploads` | `C -Q "atime \"Sun, 27 Sep 2026 12:34:56 GMT\" /a.txt" Q/a.txt` | 0 |
| `S` | `C -Q "mtime \"Sun, 27 Sep 2026 12:34:56 GMT\" /a.txt" Q/a.txt`; `-Q "rm /a.txt"` | 21 each (uploads off) |
| `S` | `C -Q pwd Q/a.txt` | 0, stdout `hello world` LF; surl's notes show no request for it |
| `S` | `C -Q "statvfs /" Q/a.txt`; `C -Q "*statvfs /" Q/a.txt` | 21; 0 |

Where a Linux or macOS build gives another exit, it is pinned per platform in its own test (root
`CLAUDE.md`) and recorded against this ADR.

## Alternatives considered

- **A home directory per account** (`/home/<user>`, or `/<user>`). Rejected: the store has one
  root, every other server serves every account the same content (ADR-0052 decision 3), and `/`
  makes `REALPATH .`, `/~/` and a relative path all mean what an absolute one does.
- **Answer a climb above `/` as absent**, as FTP does. Rejected in decision 1: `REALPATH` must be
  total and agree with every other request, and curl's own `/~/x` forms reach surl as `//x`.
- **Commit an SCP upload on the client's trailing `\0`.** Rejected in decision 4: libssh2 sends
  none, so no curl upload would ever be committed.
- **Refuse an SCP upload without `--allow-uploads` at the first acknowledgement.** Equal for curl
  (25 either way); rejected for OpenSSH's order, which answers per file on the `C` line, so the
  refusal names the file.
- **Ignore an SCP `T` line**, as curl never sends one. Rejected: OpenSSH's `scp -p` sends one to
  keep the time, and honouring it costs one store member (decision 14 item 2).
- **Only sequential SFTP writes, `OP_UNSUPPORTED` for `READ` and `WRITE` on one handle and for
  `SIZE`.** Enough for curl, which writes in order; rejected under the root `CLAUDE.md`'s default
  (a complete mate, nothing left out because it is hard): one store member (item 4) serves every
  form.
- **Answer `SETSTAT PERMISSIONS` `OK` and ignore it.** Rejected, as `SITE CHMOD` was (ADR-0052):
  the store has no mode, so success would be false; `*chmod` lets a curl user carry on.
- **Create symbolic links for `SYMLINK`.** Rejected in decision 10: a peer-made link aliases
  paths behind the operator's exposure options, and the in-memory store has no links.
- **Answer `statvfs@openssh.com`.** Rejected in decision 10: it tells a peer the host's disk
  sizes, which ADR-0006 section 3 keeps from it.
- **Replace an existing `RENAME` target**, as `ContentStore.RenameEntry` does for FTP's `RNTO`.
  Rejected: draft-02 section 6.5 makes it an error for version 3; `posix-rename@openssh.com`, the
  extension that replaces, is not advertised.
- **Report hidden create targets as `NO_SUCH_FILE`.** Rejected: `ContentStore` answers
  `NotPermitted` there by ADR-0015 decision 5, and a protocol server does not second-guess the
  store's exposure answers.

## Consequences

- BL-163 dispatches `exec` and `subsystem` by decision 2 and 5; BL-164 builds SCP, BL-165 the
  SFTP read side, BL-166 the write side and the quote commands, each from spec-derived byte
  scripts taken from this ADR; BL-171 adds the options to the `ssh` category and the `--aihelp`
  facts; BL-172 proves decision 16.
- `Surl.Content` gains decision 14's four members before BL-164 (item 2), BL-165 (item 1) and
  BL-166 (items 1 to 4) can finish; those tasks are filed separately.
- A curl upload's `--create-file-mode` and an SCP `C` line's mode are never kept; the verbose
  note says so, and a listing shows `-rw-r--r--` whatever was asked.
- A store failure after an SCP upload's data never reaches curl's exit code (decision 4).
- A `--max-message` below 30029 bytes breaks curl's SFTP uploads (decision 5).
- `--max-line` gains two uses: SCP's `exec` command and control lines (decision 12).

## Sources

- curl 8.21.0: `lib/vssh/libssh2.c` and `lib/vssh/vssh.c` at tag `curl-8_21_0`
  (https://github.com/curl/curl/tree/curl-8_21_0/lib/vssh), read 2026-09-29: `sftp_quote`,
  `sftp_quote_stat`, `sftp_upload_init`, `sftp_download_stat`, `sftp_readdir`,
  `ssh_state_sftp_*`, `ssh_state_scp_*`, `sftp_libssh2_error_to_CURLE`,
  `libssh2_session_error_to_CURLE`, `Curl_getworkingpath`, `Curl_get_pathname`.
- curl's manual as the pinned build prints it (`curl -M`, "This man page describes curl
  8.21.0"), recorded 2026-09-29; the same text at https://curl.se/docs/manpage.html: `-Q`/`--quote`
  (the SFTP command list), `-a`/`--append`, `-C`, `--create-file-mode` (default 0644),
  `--ftp-create-dirs`, `-l`/`--list-only`, `-r`/`--range`.
- libssh2 1.11.1: `src/scp.c`, `src/sftp.c`, `src/sftp.h` and `include/libssh2_sftp.h` at tag
  `libssh2-1.11.1` (https://github.com/libssh2/libssh2/tree/libssh2-1.11.1), read 2026-09-29:
  `scp_recv`, `scp_send`, `shell_quotearg`, `sftp_read`, `sftp_readdir`, `sftp_rename`,
  `LIBSSH2_SFTP_VERSION` 3, `LIBSSH2_SFTP_PACKET_MAXLEN` 256 KiB, `MAX_SFTP_READ_SIZE` and
  `MAX_SFTP_OUTGOING_SIZE` 30000, `SFTP_HANDLE_MAXLEN` 4092.
- SFTP version 3: draft-ietf-secsh-filexfer-02
  (https://datatracker.ietf.org/doc/html/draft-ietf-secsh-filexfer-02), sections 3 to 8.
- SSH channels: RFC 4254 sections 5 and 6 (`session`, `exec`, `subsystem`, `exit-status`).
- SCP: OpenSSH's `scp.c` source and sink modes (`-f`, `-t`; `T`, `C`, `D`, `E` lines; `\0`,
  `\x01`, `\x02` replies), the protocol's only definition; every form this ADR relies on is also
  one libssh2 1.11.1's `scp.c` parses or sends, read above.
