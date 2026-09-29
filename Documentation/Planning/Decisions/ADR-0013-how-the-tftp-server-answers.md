# ADR-0013 — How the TFTP server answers

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28

## Context

BL-037 gives `Surl.Protocol.Tftp.UnitLibrary` its protocol server, `TftpProtocolServer`:
reads served from the content store over a datagram flow (RFC 1350, options in RFC 2347
to RFC 2349). The task left open which options are accepted and how, the ERROR each
failure gets, the retransmission timeout and retry count, what a write request gets, and
what `netascii` mode does. Every byte had to be accepted by pinned upstream curl 8.21.0
(ADR-0003) before it was pinned.

What the pinned build sends was measured with `Record-CurlExchange.ps1 -Tftp` first
(`Surl.Protocol.Tftp.UnitTests/Fixtures/README.md`). By default curl's RRQ carries
`tsize 0`, `blksize 512` and `timeout 6`, in that order; `--tftp-blksize 1024` changes
only the `blksize` value; `--tftp-no-options` sends no option at all. curl sends the URL
path without its first `/` and percent-decoded, so `tftp://h/sub/a%20b%25c%C3%A9.txt`
sends the bytes `sub/a b%cé.txt`. With `;mode=netascii` it sends mode `netascii` and
writes the DATA bytes to its output unchanged: `a CR LF b CR NUL c` is written as those
seven bytes. A write (`-T`) sends a WRQ with `tsize` set to the upload's length. ERROR 1
makes curl exit 68 (`TFTP: File Not Found`), ERROR 2 exit 69 (`TFTP: Access Violation`).

## Decision

- **Transfer port.** Every reply, errors included, leaves from a new local port
  (`IDatagramFlow.MoveToNewLocalPortAsync`, RFC 1350 section 4). A flow that opens with
  an ERROR gets no reply and keeps its port.
- **File name to path.** One leading `/` is dropped, a `/` is put in front, and `%` and
  every byte outside printable ASCII are percent-encoded, so the content store decodes
  exactly the bytes curl sent and applies all of its refusal rules unchanged.
- **Options.** Names match in any case; only the first of a repeated name counts. The OACK
  names the accepted options in the client's order, in lower case: `blksize` from 8 up,
  above 65464 answered as 65464 (RFC 2348); `timeout` from 1 to 255 seconds, echoed
  (RFC 2349); `tsize` with any value, answered with the file's length. Anything else, or a
  value out of range or not a plain decimal number, is left out; with nothing accepted
  there is no OACK and DATA 1 comes first. The pinned build accepted the OACK
  `tsize 17 blksize 512 timeout 6` for its default request.
- **Sending.** DATA blocks go in lock step, the block number wrapping from 65535 to 0 as
  curl expects; a file whose length is a multiple of the block size ends with an empty
  DATA packet (RFC 1350 section 6), measured with a 512-byte file. `octet` and `netascii`
  both send the stored bytes unchanged: curl writes what it receives in either mode, so
  converting would put CR LF into a file the user downloads, and `tsize` stays the file's
  length. Any other mode is illegal.
- **Retransmission.** A packet is sent again when its ACK does not come within the
  negotiated `timeout`, or 5 seconds without one, on the exchange's `TimeProvider`; after
  5 retransmissions the transfer is abandoned without a further packet (the client has
  stopped listening, so an ERROR would reach no one). Five seconds is RFC 2349's typical
  range and below the 6 curl asks for; five retries keep a lost client's flow under half a
  minute at the default. An ACK of another block, such as a duplicate ACK of the block
  before, is ignored and never resent on (RFC 1123 section 4.2.3.1, the Sorcerer's
  Apprentice bug). An ERROR from the client ends the transfer; any other packet ends it
  with ERROR 4.
- **Missing and refused.** A name the store refuses, one where nothing exists, a
  directory and a file that vanishes before it is read all get ERROR 1 `File not found`,
  so a peer cannot tell them apart (ADR-0006 section 2). A file that shrinks while it is
  sent ends the transfer with ERROR 0 `The file changed while it was sent.`, so curl fails
  instead of keeping a short file that looks whole.
- **Writes.** A WRQ gets ERROR 2 `Access violation`: uploads are off by default
  (ADR-0006 section 2), and accepting them behind `--allow-uploads` is BL-054's.
- **Malformed requests.** A first datagram that is not a well-formed RRQ - no final zero
  byte, an option without a value, an unknown opcode - gets ERROR 4 `Illegal TFTP
  operation`.

## Alternatives considered

- **Converting `netascii`** (LF to CR LF, CR to CR NUL) as RFC 1350 describes: rejected,
  because upstream curl does not convert back, so every text file fetched in that mode
  would change on the way.
- **ERROR 2 for a refused name**: rejected, because it tells a peer the path exists or
  was probing outside the root, which ADR-0006 section 2 forbids.
- **An ERROR 0 when retransmissions run out**: rejected as a packet to a client that has
  gone; RFC 1350 lets a side simply stop.
