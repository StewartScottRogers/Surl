# Surl.MailStore.UnitLibrary

Phase 3.

The mail store the SMTP, IMAP and POP3 servers share (ADR-0050 decisions 2 to 7): SMTP
delivers into it, IMAP and POP3 serve from it. `MailboxStore` keeps a mailbox set per owner
in memory (BL-190): owners from the account names, or the one anonymous owner under
`--allow-anonymous`; `LookUpRecipient` maps an SMTP `RCPT TO` path to an owner, `ViewFor`
gives an IMAP or POP3 login its view, and `LockMaildrop` takes POP3's per-owner lock with a
view fixed at login. It is bounded by constants passed to its constructor (messages, total
message bytes, mailboxes, one message's bytes), and every refusal a peer can cause is a
`MailStoreOutcome`, never an exception.

The type is `MailboxStore`, not `MailStore` as ADR-0050 decision 6 writes it: a type named
`MailStore` inside the namespace `Surl.MailStore` would be shadowed by the namespace in every
other `Surl.*` namespace.

Mail domains (BL-648, ADR-0102 decisions 1.1 to 1.3): given a `MailDomainConfiguration` with
at least one domain (constructor's and `LoadAsync`'s `mailDomains`), `LookUpRecipient` answers a
recipient outside every configured domain with `RelayingDenied`, and resolves one inside through
`MailDomainRouter`: alias (targets resolved again), mailbox, account by local part, the domain's
catch-all, else `NoSuchAccount` (accepted and discarded). Domains compare in the form
`MailDomainName.TryNormalise` gives (`IdnMapping`, lower case, no trailing dot); local parts as
ADR-0050 decision 5 says (`LocalPartLookup`). Each `--mailbox` is an owner and a login named by its
normalised address. An alias or catch-all that does not resolve throws `MailRouteException` at
construction. A `MailRecipient` may name several owners (`OwnerNames`), each once. With no domain,
or under `--allow-anonymous`, resolution is ADR-0050's unchanged. The index format is unchanged.

Quotas, configured folders and shared folders (BL-675, ADR-0102 decisions 1.4 and 1.6), all
from `MailDomainConfiguration`'s `MailboxSettings` and `SharedFolders`, all ignored under
`--allow-anonymous`. A mailbox's `QuotaBytes` bounds the bytes of every message in its owner's own
mailboxes, each copy at full size; it is configuration, never persisted, and usage is counted from
the mailboxes each time. `Deliver`, `Append`, `Copy` and `Move` that would pass it store nothing and
return `QuotaExceeded` (a delivery for every recipient; a move within one owner adds nothing and
needs no quota); `CheckQuotaForAppend` answers from a length alone, for IMAP's literal `{n}`. Its
`Folders` are created at start when missing (`INBOX` skipped). Shared folders are mailboxes of the
reserved owner `:shared` (`MailboxStore.SharedFolderOwnerName`, persisted in the index like any
owner, with no `INBOX`; no account may bear the name). `ViewFor` gives each member
`Shared/<name>` (`MailView.SharedFolders`): listed, read, appended to, flagged, expunged, copied
and moved in and out by every member as equals, counted against no member's quota. A non-member
gets `MailboxMissing` for it. While any shared folder is configured, `Shared` and every name under
`Shared/` name shared folders only: a personal mailbox there is kept, unreached, and deleting or
renaming a shared folder or `Shared`, or creating or renaming to a name there, is
`SharedFolderProtected`. Configured folders or shared folders past `MaxMailboxes` throw
`InvalidOperationException` at start.

Persistence (BL-191, ADR-0050 decision 7): `MailboxStore.LoadAsync` reads the store from a
`MailStoreFiles` - `<state folder>/index` and one file per distinct message under
`messages/`, named by its file number in 16 lower-case hex digits - and refuses a malformed
store with `MailStoreLoadException` (the file at fault and the reason; the composition root
answers it with `CouldNotReadFile`, 37). `MailStoreIndex` is the index's byte format.
`SaveChangesAsync` writes the index, then deletes the files of messages no longer held; an
index write that throws leaves the change in memory and the next save writes it. A store made
by the constructor has no files and holds every message's bytes in memory. The servers call
`SaveChangesAsync` after every change and note what it throws; `Surl.Console` loads the store
(BL-207).

Streaming (BL-227, ADR-0050 decision 7): `CreatePendingMessage` gives a `PendingMessage`
whose `Body` stream writes straight into `messages/.pending-<guid>`; `Deliver` and `Append`
taking it rename that file to the message's number when they store it, delete it when they
refuse it, and refuse a pending file that cannot be created, written, closed or renamed with
`StorageFailed` (`PendingMessage.StorageFailure` holds the exception's message). A write to
`Body` never throws for a failing file, so a server can read the rest of the body off the
wire. With a data directory a message's bytes are read from its file on fetch
(`OpenMessage`, `FetchMessage`, `MaildropLock.OpenMessage` and `ReadMessage`), never loaded
at start; a POP3 maildrop lock pins its messages' files until it is released. `Deliver` takes
only a `PendingMessage` (BL-278): SMTP streams `DATA` and IMAP streams `APPEND`, so a store
with files never holds a delivered message's bytes in memory waiting for a save. The `Append`
overload taking a whole message as bytes routes it through a pending message.

Events and faults (BL-788, ADR-0124 decisions 1 and 4, ADR-0127): the constructor and `LoadAsync`
take an optional `ISurlEventPublisher` and `IFaultPlan` (null: publish nothing, fault nothing).
Every change publishes one `StoreChangedEvent` (`StoreKind.Mail`) under the store's lock, named by
`MailStoreChangeName` - `<owner>/<mailbox>` or `<owner>/<mailbox>/;UID=<uid>` - with the message's
length and never its bytes; a refused or no-op operation publishes nothing, and start-up is not a
change. `Deliver` and `Append` ask the plan at `FaultStage.StoreWrite` (account the owner, path the
mailbox) before taking the lock; an `OutOfSpace` rule refuses the whole write with `QuotaExceeded` and
the store publishes the `FaultAppliedEvent`. A plan answer that is still pending or failed is served as
no rule (BL-868 makes the writes async).

Seed, reset, snapshot and restore (BL-799, ADR-0124 decision 3, ADR-0131), in
`MailboxStore.Control.cs`: `Seed` resolves a recipient as `LookUpRecipient` does and delivers only a
deliverable one through the delivery path, skipping the fault plan and publishing `Seeded`
(`MailSeedResult`). `Reset` recreates exactly the mailboxes each owner had when the constructor
finished, empty, with new `UIDVALIDITY`s; `TakeSnapshot` gives a `MailStoreSnapshot` (not
`MailboxSnapshot`, IMAP's view of one mailbox) sharing message bytes; `Restore` takes back only this
store's snapshot, under new `UIDVALIDITY`s. A store made by `LoadAsync` refuses all three with
`InvalidOperationException`. Reset and restore publish one event named by the empty string and release
every POP3 maildrop lock; `MaildropLock` removes a message only while its UID still holds the bytes it
was given, and releases the lock only while it still holds it.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and
`Surl.Content.UnitLibrary` (for `IContentFileSystem`) and nothing else (ADR-0050 decision 1,
amending ADR-0002's table); protocol servers may reference it, and the SMTP, IMAP and POP3
servers do (BL-198, BL-201, BL-205).

Never touch the disk directly: read and write only through the injected
`IContentFileSystem`, so the tests need no disk, and write nothing outside the data
directory. Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or
`HttpListener`.
