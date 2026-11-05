# Surl.Content.UnitLibrary

Phase 1.

The content store every file-serving protocol server shares (ADR-0002): maps a request
path onto the served root (the data directory with `--directory`, an in-memory file
system without it, ADR-0031), lists directories, names media types, reads byte ranges,
reports sizes and modification times, decides where uploads and appends land, and
deletes, renames, creates and removes entries for FTP (`ContentChangeResult`, ADR-0052
decision 8), and for SCP and SFTP reports a file's or a directory's status, sets its last
write time, renames without replacing, and opens random-access uploads
(`OpenUploadAsync` and its `ContentUploadSession`, written and read at any offset, resized,
committed over the target or discarded) (ADR-0054 decision 14), every write needing
`--allow-uploads`. It stands where a
file protocol library stands in the Curl port: `file://` has no wire and no server, so
what curl's `file` scheme reads locally, Surl serves remotely through this library.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and nothing else
(ADR-0002); protocol servers may reference it.

A request path can never reach outside the served root. Every escape - `..`, its
percent-encoded forms, absolute paths, drive letters, UNC paths, symbolic links that point
out - has a test that proves it is refused. Never touch the disk directly: work through
the injected file-system seam, `IContentFileSystem`, so the tests need no disk.
It has two production implementations. `DiskContentFileSystem` is the only class here that
touches the disk: each member is a thin call into `System.IO`, excluded from coverage with
a justifying comment, and proved by the `Integration` tests in `DiskContentFileSystemTests`.
`InMemoryContentFileSystem` holds files and directories in memory (ADR-0031 decision 4):
safe for concurrent use, bounded by `MaxTotalBytes`, fully covered by the fast tests in
`InMemoryContentFileSystemTests`, and never touching the disk. `surl` serves it when no
`--directory` is given, and `DiskContentFileSystem` when one is.

`.surl` as the first segment of a request path is the data directory's service-state
folder (ADR-0031 decision 5) and is never served: `ContentStore` answers a read of it or
anything under it as missing, leaves it out of the root's listing, refuses an upload into
it with `ContentUploadResult.NotPermitted` (and a new name in it, from `CreateDirectory` or
`RenameEntry`, with `ContentChangeResult.NotPermitted`), and treats a symbolic link resolving into it as
missing - whatever `ContentExposureOptions.ServeDotFiles` and `FollowSymbolicLinks` say.
The match is case-insensitive on every platform and whole-segment only: `.surlx`,
`.surl-upload-<guid>` and `sub/.surl` are ordinary dot-files. Never weaken this rule; the
rule holds in memory too, so an upload can never create a `.surl`.

`ObjectMetadataIndex` keeps what a file system cannot hold for the object-storage servers
(S3, Azure Storage, Cloud Storage, the OCI registry; ADR-0100 decision 2.3, built by BL-636):
the five header fields set at upload, user metadata, the ETag and GCS's generation, keyed by
the file's path relative to the served root (`bucket/x/y`) and stamped with the file's length
and last write time. A stamp that no longer matches - the file was overwritten, renamed or
deleted through the content store by any protocol - makes the entry absent: the file answers
the defaults (`DefaultContentType`, no user metadata, the quoted lower-case hexadecimal MD5 of
its bytes as ETag, cached on first read, and its last write time in microseconds since the
Unix epoch as generation). Bounds: `MaxEntries` (default 100000, `MetadataIndexFull`, stale
entries dropped first) and `MaxMetadataBytes` (default 8192, the UTF-8 bytes of the header
fields and user metadata names and values, `MetadataTooLarge`). With a data directory it is
`<root>/.surl/objects/index`, rewritten whole through `.index-<guid>` and
`MoveFileReplacing`, loaded by `LoadAsync` (malformed: `ObjectMetadataIndexLoadException`,
reason `not an object metadata index`); a failed write is returned as `StorageFailure`, never
thrown. Its byte format is pinned in `ObjectMetadataIndexFormat`'s remarks: the header
`SURL-OBJECT-INDEX-1` and LF, a 4-byte entry count, then entries in strictly ascending ordinal
key order - key, length (8, signed), last write UTC ticks (8, signed), generation (8, signed),
ETag, the five header fields (empty is absent), a 2-byte user-metadata count and its name and
value pairs in strictly ascending ordinal order of name; every string is a 2-byte UTF-8
length then its bytes, every integer big-endian.
