# ADR-0018 — A trailing slash after a file, and a served root that is not a full path

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28

## Context

The code review of BL-008 found two behaviour gaps in `ContentStore` (BL-045):

1. `MapRequestPath("/file.txt/")` mapped to the file, with `ContentEntryKind.File`. A POSIX
   file system refuses `file.txt/` with `ENOTDIR`; Windows strips the slash and opens the
   file. The store answered as Windows does, on every platform.
2. The constructor documented that the served root is a full path but did not check it, so
   a root such as `srv` or `C:` made every `Location` depend on the current directory.

Measured against pinned upstream curl 8.21.0 (win-x64 reference build) with
`Record-CurlExchange.ps1`, `curl -s -o NUL -w %{http_code} http://127.0.0.1:18745/file.txt/` sends the path as
written:

```
GET /file.txt/ HTTP/1.1
Host: 127.0.0.1:18745
User-Agent: curl/8.21.0
Accept: */*
```

Upstream curl neither strips nor adds the slash, so what the path names is the server's
decision. Answered with a canned 404, the same run printed `404` for `-w %{http_code}`
and exited 0.

## Decision

1. **A trailing `/` names a directory.** A request path ending in `/` whose location holds
   a file maps with `IsMapped` set and `EntryKind` `None`: answered exactly as a path that
   does not exist, as POSIX answers it, on every platform. The mapping remembers that the
   path named a directory, so `GetEntryKind`, `GetFileStatus` and `ListDirectory` keep
   answering a file there as nothing, `CopyFileBytesAsync` throws
   `FileNotFoundException`, and `WriteUploadAsync` returns `NotPermitted` whether or not
   anything is there yet (an upload never creates `new.txt` from `/new.txt/`). A directory
   answers with or without the trailing `/`. `Location` is still the joined path, so a
   protocol server can answer it as it answers any missing path.
2. **The served root must not depend on the current directory.** The constructor throws
   `ArgumentException` for a relative root (`srv`, `srv/www`) and, on Windows, a
   drive-relative one (`C:`, `C:srv`). It accepts a fully qualified path and a path that
   starts with a directory separator. On Windows `/srv/www` is rooted on the current drive
   rather than fully qualified; it is accepted because it does not move with the current
   directory, and because every test in the solution spells its root that way to stay
   platform-neutral. The root is kept as given, never rewritten: `Surl.Console` already
   passes `Path.GetFullPath` of the served directory.

## Consequences

- Every protocol server that answers a missing path answers `/file.txt/` the same way,
  with no change of its own.
- A caller that builds a `ContentStore` from a relative path now fails at construction,
  not with a `Location` that silently depends on where the process started.
- Tests pin the constructor's platform-specific answers separately:
  `\srv\www` is accepted on Windows and refused as relative elsewhere.
