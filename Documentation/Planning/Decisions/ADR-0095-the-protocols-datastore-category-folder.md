# ADR-0095 — The `Protocols/DataStore/` category folder

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Stewart, 2026-11-04, approved `Protocols/DataStore/` as the category folder for
  the Redis server (recorded in [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md),
  "Stewart's approval", "Layout", and its server table). Decided by Claude under Stewart's
  delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04, in BL-400, for the wording of the
  amendment and what the folder holds.
- **Amends:** [ADR-0079](ADR-0079-projects-are-grouped-in-category-folders.md) decision 1 (a new
  row in its table)

## Context

ADR-0079 decision 3 says a new category folder is decided by an ADR. ADR-0083 places the
extended server `Surl.Extended.Protocol.Redis` (`redis`, `rediss`) in `Protocols/DataStore/`, a
folder ADR-0079's table does not have, and leaves adding it to this task.

ADR-0079 decision 2 groups projects by job, not by wire shape. Redis's job is to hold data - keys
and their values - and hand it back on request. Two existing categories come close:

- `Protocols/Messaging/` holds `Surl.Protocol.Ldap` and `Surl.Protocol.Mqtt`. Redis has
  publish and subscribe, but that is a side feature of a key-value store, not its job; the
  Curl port's `redis` scheme reads and writes keys. Placing Redis by its pub/sub would be
  placing it by one command family rather than by what it is for.
- `Protocols/Lookup/` holds `Surl.Protocol.Dict` and `Surl.Protocol.Gopher`, read-only
  services answering a query. RESP is request-reply like them, but that is wire shape, which
  ADR-0079 decision 2 rules out as a reason, and a Redis client writes as well as reads.

## Decision

1. **ADR-0079 decision 1's table gains a row:**

   | Folder | Projects |
   | --- | --- |
   | `Protocols/DataStore/` | `Surl.Extended.Protocol.Redis` |

   In ordinal order it is the first of the `Protocols/` folders: `DataStore`, `FileTransfer`,
   `Http`, `Lookup`, `Mail`, `Messaging`, `Terminal`.
2. **`Protocols/DataStore/` is for servers whose job is storing and serving keyed data**:
   Redis now, and any later key-value or database server. A library shared only by
   data-store servers goes beside them, as ADR-0079 decision 3 says for every category.
   The object stores (S3, Azure Storage, Google Cloud Storage, the OCI registry) stay in
   `Protocols/FileTransfer/`, where ADR-0083's table puts them: their job is moving whole
   files, not serving keyed values. This ADR moves no project.
3. **The folder appears with its first project.** No empty folder is committed. The Redis
   scaffold task (BL-488) creates the directory, the projects, and the `/Protocols/DataStore/`
   solution folder in `Surl.slnx` (ADR-0079 decision 4). This ADR changes no project.

## Consequences

- `CLAUDE.md`, "Repository layout", and the product overview's "Project layout" name
  `DataStore/` among the `Protocols/` folders. The product overview is updated with this ADR;
  `CLAUDE.md`'s tree gains the folder when BL-488 creates it, so the tree only lists folders
  that exist.
- The board's overlap check is unaffected: tasks name the Redis projects by their category
  path, `Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary`.

## Alternatives considered

- **`Protocols/Messaging/`.** Redis has pub/sub. Rejected: categories are by job, and a
  Redis server's job is a data store; pub/sub is one of its many command families.
- **`Protocols/Lookup/`.** RESP is request-reply. Rejected: that is wire shape, and Lookup's
  servers are read-only, where Redis is read-write.
- **A broader `Protocols/Storage/`** for Redis and the cloud object stores together.
  Rejected: ADR-0083, approved by Stewart, already places the object stores in
  `Protocols/FileTransfer/` and names Redis's folder `Protocols/DataStore/`.
