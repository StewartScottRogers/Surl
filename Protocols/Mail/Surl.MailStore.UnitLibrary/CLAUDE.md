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
construction. After `AddOwner` or `RetireOwner`, a target that no longer names an owner adds none and
every other target still resolves, in any order (BL-2121, ADR-0185's 2026-11-28 note); an alias none
of whose targets leads to an owner is `NoSuchAccount`. A `MailRecipient` may name several owners (`OwnerNames`), each once. With no domain,
or under `--allow-anonymous`, resolution is ADR-0050's unchanged. The index has no field for mail domains.

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
Folder uses (BL-1976, ADR-0192): the index keeps each mailbox's RFC 6154 use as one byte under the
header `SURL-MAIL-INDEX-2`, written only when some mailbox holds a use; a store with none writes
`SURL-MAIL-INDEX-1` with no use byte, and both are read. At start a configured use is given to its
folder, loaded or new, replacing the folder's use and leaving any other mailbox of the owner that
held it with none (a change, written by the next save; loading itself writes nothing); a folder
configured with no use keeps the loaded one unless another folder is configured with that use.
`HoldsAnyFolderUse` says whether any mailbox of any owner holds a use at the moment it is read;
the IMAP server reads it once, when it is constructed, to decide whether it offers `SPECIAL-USE`
for its whole life (ADR-0055, Amendment 2).
`SaveChangesAsync` writes the index, then deletes the files of messages no longer held; an
index write that throws leaves the change in memory and the next save writes it. A store made
by the constructor has no files and holds every message's bytes in memory. The servers call
`SaveChangesAsync` after every change and note what it throws. `Surl.Composition`'s
`SurlServerComposition.LoadMailStoreAsync` loads the store (BL-207, BL-609): with `--directory`,
through `MailboxStore.LoadAsync` over the `MailStoreFiles` that `ComposeMailStoreFiles` gives
(`<data directory's full path>/.surl/mail`); without it, it makes a `MailboxStore` with the
constructor, held in memory only.

Streaming (BL-227, ADR-0050 decision 7): `CreatePendingMessage` gives a `PendingMessage`.
In a store with files its `Body` stream writes straight into the pending file
`messages/.pending-<guid>`; `Deliver` and `Append` taking it rename that file to the message's
number when they store it, delete it when they refuse it, and refuse a pending file that cannot
be created, written, closed or renamed with `StorageFailed` (`PendingMessage.StorageFailure`
holds the exception's message). A write to `Body` never throws for a failing file, so a server
can read the rest of the body off the wire. A store without files (no data directory) creates no
pending file: it buffers the pending message in memory, and a stored message's bytes are taken
from that buffer. With a data directory a message's bytes are read from its file on fetch
(`OpenMessage`, `FetchMessage`, `MaildropLock.OpenMessage` and `ReadMessage`), never loaded
at start; a POP3 maildrop lock pins its messages' files until it is released. `Deliver` takes
only a `PendingMessage` (BL-278): SMTP streams `DATA` and IMAP streams `APPEND`, so a store
with files never holds a delivered message's bytes in memory waiting for a save. The `Append`
overload taking a whole message as bytes routes it through a pending message.

Events and faults (BL-788, ADR-0124 decisions 1 and 4, ADR-0127): the constructor and `LoadAsync`
take an optional `ISurlEventPublisher` and `IFaultPlan` (null: publish nothing, fault nothing).
Writes publish `StoreChangedEvent`s (`StoreKind.Mail`) under the store's lock, named by
`MailStoreChangeName` - `<owner>/<mailbox>` or `<owner>/<mailbox>/;UID=<uid>`; a message's event
carries its length and never its bytes, a mailbox's event no byte count. One operation is not always
one event. `Append`, and a `ChangeFlags` that alters the flags, publish one. A delivery and `Seed`
publish one per owner copy (each owner of each recipient entry or `MailOwnerCopy`). `Expunge` and
`MaildropLock.RemoveMessages` publish one per message removed. `Copy` publishes one per message
copied; `Move` two per message moved (its copy `Written`, its original `Deleted`). `CreateMailbox`
publishes one, and so does `DeleteMailbox`, with none for the messages the mailbox held.
`RenameMailbox` publishes two (old name `Deleted`, new name `Written`), or one for `INBOX` (new name
`Written`), and none per message. `Reset` and `Restore` publish one each, named by the empty string.
`AddOwner` publishes one for a new owner (`Created`, naming its `INBOX`) and none when it reaches a
retired owner again; `RetireOwner` publishes none. A refused operation, or one with nothing to do (no
recipient, no matching UID, flags already as asked), publishes no `StoreChangedEvent`. Nothing the
constructor or `LoadAsync` does at start-up is published, although `LoadAsync` counts an `INBOX` or
configured folder it creates, and a configured use it gives, in `ChangeCount`, and the next save
writes it (the constructor leaves `ChangeCount` at 0).
Deliveries and appends (`Seed` excepted) ask the plan at `FaultStage.StoreWrite` (account the owner,
path the mailbox) outside the store's lock; `Copy` and `Move` never ask it. An `OutOfSpace` rule
refuses the whole write with `QuotaExceeded` and the store publishes the `FaultAppliedEvent`. The
synchronous writes serve a plan answer that is still pending or failed as no rule; `DeliverAsync` and
`AppendAsync` (BL-868, ADR-0127 amendment 1; `AppendAsync` returns a `MailAppendResult`) await the
plan, so an override answering later can refuse the write. SMTP and IMAP use them.

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

Owners at run time (BL-1451, ADR-0185): `AddOwner(name)` makes an owner with an empty `INBOX` (not counted
against `MaxMailboxes`; one `Created` event; one `ChangeCount` change, in every store) or reaches an owner held but
unreached again with its mail (no event, no change); `RetireOwner(name)` makes an account's owner unreached (mail kept
and counted against the bounds; no event, no change; `false` for a mailbox address). Both answer `false` and do nothing
for the cases ADR-0185 decision 6 lists. A retired name is not necessarily an empty view or `NoSuchAccount`: `ViewFor`
still gives it its shared folders, `LookUpRecipient` (so `Seed`) can still deliver its path through an alias or mailbox
of that address, an account differing only by case or a mail domain's catch-all (and answers `RelayingDenied` outside
the mail domains), and views and recipients given before the call still reach the owner. `Reset` drops owners not
configured and reaches configured ones again; `TakeSnapshot` records `OwnerCount` and which owners are reached, and
`Restore` puts both back.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and
`Surl.Content.UnitLibrary` (for `IContentFileSystem`) and nothing else (ADR-0050 decision 1,
amending ADR-0002's table); protocol servers may reference it, and the SMTP, IMAP and POP3
servers do (BL-198, BL-201, BL-205).

Never touch the disk directly: read and write only through the injected
`IContentFileSystem`, so the tests need no disk, and write nothing outside the data
directory. Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or
`HttpListener`.

Per-owner copies and `DeliveredFor` (BL-1500, ADR-0053 Amendment 1 decisions 3, 4, 5 and 7): `Deliver(IReadOnlyList<MailOwnerCopy>)`
and `DeliverAsync` take one `MailOwnerCopy` per owner (the recipient, its own `PendingMessage`, its `DeliveredFor`
addresses) and store them all or none with the same refusals as the single-message overloads (`MessageTooLarge`,
`StorageFailed`, `StoreFull`, `QuotaExceeded`, the out-of-space fault; a quota sums every copy an owner gets), disposing
every pending message whatever the outcome. If a later copy's file cannot be kept, the earlier files are released (their
file numbers are not reused). One batch is one change. Every `Delivered` event carries `DeliveredFor`: the copy's list, in the
order given; the single-message overloads publish the addresses a `MailRecipient` records through
`MailRecipient.ForAddresses`, else null (as before). `MailboxStore.DeliveredToEnabled` (init-only, default false; `LoadAsync`'s `deliveredToEnabled` sets it) is only
carried here: the composition root sets it and the SMTP server reads it. Neither `DeliveredFor` nor `DeliveredToEnabled` is written to the index.

Seeded `DeliveredFor` (BL-1515, ADR-0053 Amendment 1): `Seed` publishes each seeded copy's `Seeded` event with `DeliveredFor` holding the one `recipientPath` address with its angle brackets removed (an alias target's copies all name the alias address that was seeded). The bytes stored are unchanged; seeding never writes a `Delivered-To` header.
