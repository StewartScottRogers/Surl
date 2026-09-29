# ADR-0015 — How the content store applies the exposure options and the upload limit

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28

## Context

ADR-0006 section 2 decides what a server exposes by default - uploads refused, listings
refused, symbolic links not followed, dot-files hidden - and section 1 caps an upload at
104857600 bytes, "checked again by `Surl.Content` as it writes". Section 6 has
`Surl.Content` take these as constructor options "so every protocol server gets the same
answers". BL-047 adds them. Several choices were left open: how "answered as absent"
survives later looks at a mapped path, what an upload to a hidden path gets, how to add
the options without breaking the servers other dark factory lanes are building on the
store, and how the seam gains a write path without breaking their test fakes.

No byte on the wire is decided here. Each protocol server measures its answer to these
results against pinned upstream curl in its own task (ADR-0003).

## Decision

1. **The options type.** `ContentExposureOptions`, a record with `AllowUploads`,
   `ListDirectories`, `FollowSymbolicLinks`, `ServeDotFiles` (all `false`) and
   `MaxUploadBytes` (104857600; 0 is no limit; a negative value throws). A new instance is
   ADR-0006's defaults. `ContentStore(string, IContentFileSystem, ContentExposureOptions)`
   takes it.
2. **Answered as absent.** A path with a segment starting with `.` (dot-files hidden), or a
   path whose resolution passes through a symbolic link inside the root (links not
   followed), maps to a mapping with `IsMapped` set, `EntryKind` `None` and `Location`
   the path joined to the resolved root - the shape of a path that does not exist. The
   mapping remembers internally that it is hidden, so `GetEntryKind` says `None`,
   `GetFileStatus` says `null`, `ListDirectory` says `None` and `CopyFileBytesAsync`
   throws `FileNotFoundException`, without asking the seam. A link is detected by
   comparing the resolved path with the path joined to the resolved root, so a served
   root that is itself a link still serves its files.
3. **Outside the root is still refused.** The `ResolvesOutsideRoot` check runs before the
   exposure rules, so a link out of the root is refused whatever the options, as before.
4. **Listings.** With `ListDirectories` off, `ListDirectory` answers every mapping with
   `LocationKind` `None` and asks the seam nothing. With it on, a listing leaves out
   dot-files unless `ServeDotFiles` is on and links inside the root unless
   `FollowSymbolicLinks` is on.
5. **Uploads.** `WriteUploadAsync(mapping, source, token)` returns a
   `ContentUploadResult`: `Written`, `NotPermitted` or `TooLarge`. It is `NotPermitted`,
   with nothing created, when uploads are off, when the path is hidden, when the location
   is a directory, or when its parent is not an existing directory. An upload to a hidden
   path is refused rather than written: writing it would put bytes where the store claims
   nothing is, or overwrite a hidden file. That tells a peer with uploads on that the
   path is special, which is the lesser leak.
6. **The limit.** Bytes are counted as read, and each read asks for at most one byte past
   the limit, so the store never reads more than the limit plus one byte. Crossing it
   stops the copy, deletes the partial file through the seam and returns `TooLarge`. A
   read or write that throws, cancellation included, also deletes the partial file before
   the exception goes on. The upload writes the target file in place, so a replaced file
   is gone after a refused or failed upload; the peer was allowed to replace it.
7. **The seam's write path.** `IContentFileSystem` gains `CreateFileForAsyncWrite` and
   `DeleteFile` as default interface members that throw `NotSupportedException`, so a
   read-only seam - the test fakes in the HTTP, Gopher, DICT and TFTP projects - compiles
   unchanged. `DiskContentFileSystem` implements both.
8. **The two-argument constructor stays for now.** `ContentStore(string,
   IContentFileSystem)` serves with `ContentExposureOptions.ServeEverythingInsideTheRoot`:
   listings, dot-files and links on, uploads off - exactly what the store did before it
   took options. `Surl.Console` and the HTTP, Gopher, DICT and TFTP tests use it, and
   BL-047 may not touch them. BL-070 passes the options `Surl.Cli` parses from
   `Surl.Console`; BL-069 moves every other caller to the three-argument constructor and
   removes the two-argument one.

## Consequences

- Until BL-070, `surl` still serves with everything inside the root exposed, as it did
  before BL-047.
- DICT's `MATCH` is not a listing (ADR-0011, section 4), so since BL-081 it reads its
  database with `ListDirectoryWhateverTheListingSwitchSays`, which applies the dot-file
  and symbolic-link rules but not `ListDirectories`.
- Protocol servers answer `NotPermitted` and `TooLarge` with their own codes, measured
  against pinned upstream curl in their own tasks.
