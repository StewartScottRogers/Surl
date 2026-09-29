# ADR-0012 — How the Gopher server answers a selector

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28

## Context

BL-034 gives `Surl.Protocol.Gopher.UnitLibrary` its protocol server, `GopherProtocolServer`:
the selector a client sends answered from the content store (RFC 1436). The task left
open how a selector maps onto the content store, the item type of each menu entry, the
host and port a menu names, what a refused or missing selector gets, and what a file
reply looks like. Every byte had to be accepted by pinned upstream curl 8.21.0
(ADR-0003) before it was pinned.

What the pinned build sends was measured with `Record-CurlExchange.ps1 -Raw` first
(`Surl.Protocol.Gopher.UnitTests/Fixtures/README.md`): the URL path without its first
character (the item type), URL-decoded, then CRLF; the empty selector for the root; a
`%09` becomes a TAB followed by the search words. Without `--path-as-is` curl removes dot
segments, percent-encoded ones included, before sending; with it, or with `%252e`, a
`..` reaches the server. curl writes every byte of the reply to its output until the
server closes, with no un-stuffing and no check of an item type.

## Decision

- **Selector to path.** The line ends at LF, with or without CR. Anything after the first
  TAB is ignored and the selector before it is answered: the content store has no search
  engine, and Gopher+ clients send `selector TAB +`, which then still gets the item. The
  selector is read as a percent-encoded path - bytes outside printable ASCII are
  percent-encoded, a leading `/` is added when missing - and the content store maps it,
  so every refusal rule it has (a `..` raw or percent-encoded, a separator or control
  character in a segment, a device name) applies to Gopher unchanged.
- **Files** are sent as their bytes exactly, with no terminating `.` line and no line-ending
  conversion, for every item type: curl would write the `.` line into the user's file.
  The close marks the end. If a file shrinks while it is sent, the connection is aborted,
  so the client sees a reset instead of a short file that looks whole.
- **Menus** list the content store's entries in its order (ADR-0009), each
  `type name TAB selector TAB host TAB port CRLF`, then `.` CRLF. Type `1` for a directory;
  for a file by extension, `0` (`.txt`, `.text`, `.md`, `.csv`, `.log`), `g` (`.gif`),
  `I` (`.png`, `.jpg`, `.jpeg`, `.bmp`), `h` (`.html`, `.htm`), otherwise `9`, binary -
  the one type a client never alters. The selector is the directory's path, `/` and the
  name with `%` and every byte outside printable ASCII percent-encoded, so each selector
  maps back to exactly its entry. The host is the listen URL's host, except that the
  wildcard `0.0.0.0` or `::` is replaced by the connection's local address (IPv4-mapped
  addresses as IPv4, IPv6 without its zone), because no client can connect to a wildcard; the port is the
  listen URL's bound port.
- **Refused, missing and vanished selectors** all get one error menu, fixed text with no
  echo of the selector (ADR-0006 section 3), so a peer cannot tell them apart (ADR-0006
  section 2): `3Nothing is served at this selector.` TAB TAB `error.host` TAB `1` CRLF
  `.` CRLF - item type `3` as RFC 1436 defines it, with the host and port gopher servers
  conventionally give an error item. curl 8.21.0 exits 0 and writes the menu.
- **Limits** (ADR-0006 sections 1 and 5): a line past `MaxLineBytes`, line ending
  included, a line not complete within `HeadTimeout`, and a client that closes before a
  whole line, all get no bytes and a close.
- **Exposure** (listings off, dot-files hidden) is not applied here: BL-047 applies it in
  `ContentStore` for every protocol server, and this server answers whatever the store
  answers as absent with the error menu.

## Alternatives considered

- **Answer a search (TAB) with the error menu.** Rejected: it would refuse every Gopher+
  client's request, and no item Surl lists is a search server anyway.
- **Type `0` with a `.`-terminated, dot-stuffed body for text files** (RFC 1436 section 4).
  Rejected: curl writes those bytes to its output, so the file fetched would not be the
  file served.
- **Selectors as raw file names, no percent-encoding.** Rejected: a `%` in a name, a byte
  outside ASCII, or a TAB-free but control-bearing name would have no safe spelling, and
  the content store's refusal rules would need a second, Gopher-only decoder.

## Consequences

- `gophers` is the same server over TLS; it joins `Schemes` with the TLS contract
  (ADR-0010), in a later task. Wiring `gopher` into `surl` and the live conformance run
  are BL-040.
- A later media-type table in `Surl.Content` should replace the extension table here.
