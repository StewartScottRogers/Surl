# Surl.Content.UnitLibrary

Phase 1.

The content store every file-serving protocol server shares (ADR-0002): maps a request
path onto the served root (the data directory with `--directory`, an in-memory file
system without it, ADR-0031), lists directories, names media types, reads byte ranges,
reports sizes and modification times, and decides where uploads land. It stands where a
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
it with `ContentUploadResult.NotPermitted`, and treats a symbolic link resolving into it as
missing - whatever `ContentExposureOptions.ServeDotFiles` and `FollowSymbolicLinks` say.
The match is case-insensitive on every platform and whole-segment only: `.surlx`,
`.surl-upload-<guid>` and `sub/.surl` are ordinary dot-files. Never weaken this rule; the
rule holds in memory too, so an upload can never create a `.surl`.
