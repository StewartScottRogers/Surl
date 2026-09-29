# ADR-0009 — What a content-store directory listing holds

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28

## Context

ADR-0002 gives `Surl.Content` the job of listing directories, so a Gopher menu (BL-034),
an HTTP directory index, FTP `LIST`/`NLST`/`MLSD`, SFTP `READDIR` and SMB directory queries
all read one listing. ADR-0006 section 2 decides what a server exposes by default -
listings off, dot-files hidden, symbolic links not followed - and BL-047 applies those
rules as `ContentStore` options. BL-044 adds the listing itself, before those options
exist. This ADR fixes what the listing contains, so every protocol server lists the same
entries whatever the platform.

No byte on the wire is decided here: each protocol server formats the listing its own way
and measures that format against pinned upstream curl in its own task (ADR-0003).

## Decision

`ContentStore.ListDirectory(ContentPathMapping, CancellationToken)` returns a
`ContentDirectoryListing`:

1. **What is at the location.** A directory is listed. A file or nothing is a result that
   says which (`LocationKind`), with no entries, never an exception. A refused mapping is
   an `ArgumentException`, as for every other read of the store.
2. **Each entry** carries its name, `File` or `Directory`, a file's length (none for a
   directory) and its last modification time in UTC.
3. **Order is ordinal by name** (`string.CompareOrdinal`, UTF-16 code unit by code unit,
   case-sensitive), whatever order the file system returns. The same tree lists the same
   way on Windows, Linux and macOS; a culture-aware or case-insensitive order would not.
4. **Left out:**
   - an entry whose name `MapRequestPath` would refuse as a segment (`CON`, `a:b`,
     `name.`, a control character, a backslash). Every listed name can be asked for, so a
     protocol server never offers a link it would then refuse, and a Linux file named
     `a:b` does not appear on one platform only;
   - a symbolic link whose final target resolves outside the served root, by the same
     ordinal comparison `MapRequestPath` uses;
   - an entry where nothing is found by the time it is looked at: a dangling link, or an
     entry deleted between enumeration and its status.
5. **Kept:** a symbolic link whose final target is inside the root, under its own name,
   with its target's kind, length and modification time; and dot-files. Hiding dot-files
   and unfollowed links, and refusing the listing itself, are ADR-0006's defaults, which
   BL-047 applies on top of this listing as `ContentStore` options.
6. **Entries skipped for attributes: none.** `DiskContentFileSystem` enumerates with
   `AttributesToSkip = 0`, so a Windows hidden or system entry is listed as the same entry
   is on Linux and macOS; an entry the process may not read is skipped.
7. **Cancellation** is checked before the directory is read and before every entry.

## Alternatives considered

- **List unrequestable names and let each protocol server drop them.** Rejected: every
  server would need the same rule, and one that forgot would list names it then refuses.
- **Culture-aware or case-insensitive order.** Rejected: it differs by platform and by
  culture, and a listing must be byte-for-byte stable across CI's three platforms.
- **Hide dot-files here now.** Rejected: ADR-0006 makes that an option (`--serve-dot-files`),
  and options land in `ContentStore` with BL-047; hard-coding it here would have to be
  undone there.

## Consequences

- BL-047 filters this listing by ADR-0006's exposure options.
- Each protocol server that lists a directory reads `ContentDirectoryListing.Entries` and
  formats them; none enumerates the disk itself.
