# Surl.Content.UnitLibrary

Phase 1.

The content store every file-serving protocol server shares (ADR-0002): maps a request
path onto the served directory, lists directories, names media types, reads byte ranges,
reports sizes and modification times, and decides where uploads land. It stands where a
file protocol library stands in the Curl port: `file://` has no wire and no server, so
what curl's `file` scheme reads locally, Surl serves remotely through this library.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and nothing else
(ADR-0002); protocol servers may reference it.

A request path can never reach outside the served root. Every escape - `..`, its
percent-encoded forms, absolute paths, drive letters, UNC paths, symbolic links that point
out - has a test that proves it is refused. Never touch the disk directly: work through
the injected file-system seam, `IContentFileSystem`, so the tests need no disk.
`DiskContentFileSystem` is its one real implementation and the only class here that
touches the disk: each member is a thin call into `System.IO`, excluded from coverage with
a justifying comment, and proved by the `Integration` tests in `DiskContentFileSystemTests`.
