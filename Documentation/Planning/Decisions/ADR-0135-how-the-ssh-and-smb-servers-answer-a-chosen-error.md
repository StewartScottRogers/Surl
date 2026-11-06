# ADR-0135 — How the SSH and SMB servers answer a chosen error

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-795.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 4 (the fault model, the `error`
  kind and its reply table), [ADR-0054](ADR-0054-how-the-ssh-server-answers-upstream-curls-scp-and-sftp-requests.md)
  (SCP and SFTP), [ADR-0073](ADR-0073-how-the-smb-server-answers-upstream-curl-and-checks-its-ntlmv1-session-setup.md) (SMB),
  [ADR-0134](ADR-0134-how-the-http-rtsp-and-websocket-servers-answer-a-chosen-error.md) (the same question for
  HTTP, RTSP and WebSocket, whose decisions 4 to 6 this one follows).
- **Acted on by:** BL-795.

## Context

ADR-0124 decision 4 says each server family answers a matching `error` rule at the `Command` stage with the
rule's `reply` in the protocol's own error shape: for SFTP "a status 2 to 8 and text", for SCP "a message line
sent after byte 1", for SMB "an NTSTATUS error as eight hex digits"; the command is the SFTP packet name,
`scp`, or the SMB command, and the path the file path. It does not say where in each server the plan is asked,
what path a handle-based request or an SMB tree carries, or what the bytes are.

## Decision

1. **SFTP** (`SftpSession`). The plan is asked once per request after `INIT`, before the request is read and
   before the content store, with scheme `sftp`, the logged-in account (none when anonymous), the packet name
   (`OPEN`, `READ`, `REMOVE` ...) and a path: for a request whose first field is a path, that path made
   canonical as ADR-0054 decision 1 reads it, without a trailing `/`; for `CLOSE`, `READ`, `WRITE`, `FSTAT`,
   `FSETSTAT` and `READDIR`, the path the handle was opened on; none for a handle not open, a path that is not
   UTF-8 or a field that runs past the packet, and none for `EXTENDED` or an unknown type. A chosen error is
   `SSH_FXP_STATUS` with the request's id, the reply's code, its text as the message and language tag `en`, and
   the session goes on. An upload's `OPEN` so refused opens nothing, so the store is unchanged.
2. **SCP** (`ScpUploadHandler`, `ScpDownloadHandler`). The plan is asked once per SCP command, with scheme
   `scp`, command `scp` and the command's path made canonical: for an upload before the first `\0` (libssh2's
   `scp_send` reads that byte as the command's acknowledgement), for a download once the client's starting
   `\0` arrives and before the store is asked. A chosen error is byte 1, the reply exactly as given - no
   `scp: ` before it, unlike surl's own refusals, so the test chooses every byte of the line - and LF, then
   exit status 1, as every other SCP refusal ends.
3. **SMB** (`SmbSession`). The plan is asked once per request after the session setup, before the tree and
   file checks and the content store, with the listen URL's scheme (`smb` or `smbs`), the logged-in account
   and the command named `TREE_CONNECT`, `CREATE`, `READ`, `WRITE`, `CLOSE` or `TREE_DISCONNECT`. The path is
   `/<share>` for a tree connect or disconnect, `/<share>/<name>` with the name's backslash-separated parts
   joined by `/` for a create, the open file's for a read, write or close, and none for a tree or file the
   session does not hold. A chosen error is the empty error response every refusal is (ADR-0073), carrying the
   reply's NTSTATUS, with `SMB_FLAGS2_NT_STATUS` (`0x4000`) added to the echoed `Flags2` so the status field
   says what it holds ([MS-CIFS] 2.2.3.1); every other response keeps its DOS-class status and the request's
   `Flags2`. An upload's `CREATE` so refused opens nothing, so the store is unchanged.
4. **What fits.** SFTP reads a status 2 to 8, one space and non-empty text; SCP a non-empty line with no CR or
   LF; SMB eight hex digits at `0xC0000000` or above. A reply that does not fit - which a `Surl.Api` hook could
   hand over unchecked, and the SCP line-break case that `Surl.Cli`'s check lets through - is noted in the
   exchange log and not applied: the request is answered as without the rule and no event is published
   (ADR-0134 decision 4).
5. **Only the `error` kind.** As ADR-0134 decision 5: a `delay` or `drop` rule given `stage=command` that
   matches is noted as not applied and the request goes on. Applying those at the command stage needs the
   engine's idle clock paused and an abort from inside a channel, filed as BL-909.
6. **Events and the point.** Each application publishes one `FaultAppliedEvent` (stage `Command`, the
   exchange's ID, the path as its target, or the command when there is no path) and notes the answer. The
   `FaultPoint` carries the exchange ID, the listen URL formatted as the serving engine formats it (bound port
   included), the scheme, the account, the path and the command. `SshCommandFaults` and `SmbCommandFaults`
   build the point, read the reply and publish; each library has its own because protocol servers share no
   code but the horizontal libraries.

## What upstream curl does with these answers

The bytes are those a natural refusal already sends, which pinned upstream curl 8.21.0 has been measured
against in `Surl.Conformance.UnitTests`: SFTP `NO_SUCH_FILE` exits 78, `PERMISSION_DENIED` 9 and `FAILURE` 79
(`UpstreamCurlTransfersFilesWithSurlOverSftpTests`); an SCP error line exits 78 on a download and 25 on an
upload (`UpstreamCurlTransfersFilesWithSurlOverScpTests`); an SMB `CREATE` refused with any status but
`ERRDOS/ERRnoaccess` exits 78 (`UpstreamCurlTransfersFilesWithSurlOverSmbTests`). The fault's own exchanges
against a live `surl` wait on BL-803, which composes the fault plan into the running server; BL-910 measures
them then, including the two not yet seen - an SCP upload refused before its first `\0`, and an NTSTATUS
rather than a DOS-class status.

## Consequences

A test can make one SFTP request, SCP command or SMB command fail on purpose and see it on the event stream,
with the session going on (SFTP, SMB) or the command ending (SCP) as for any other refusal. No store changes.
The command-stage delay and drop (BL-909) and the live measurement (BL-910) are follow-up tasks.
